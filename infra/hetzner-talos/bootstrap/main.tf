# Bootstrap des Clusters: alles, was Geheimnisse braucht oder vor ArgoCD da sein muss.
# Danach übernimmt ArgoCD (App of Apps unter deploy/argocd) per GitOps.

data "terraform_remote_state" "cluster" {
  backend = "local"
  config = {
    path = var.cluster_state_path
  }
}

# Token + Netz-ID für Hetzner CCM und CSI (Charts lesen Secret kube-system/hcloud, Schlüssel token/network).
resource "kubernetes_secret_v1" "hcloud" {
  metadata {
    name      = "hcloud"
    namespace = "kube-system"
  }
  data = {
    token   = var.hcloud_token
    network = tostring(data.terraform_remote_state.cluster.outputs.network_id)
  }
}

resource "kubernetes_namespace_v1" "conference" {
  metadata {
    name = "conference"
    labels = {
      "app.kubernetes.io/part-of" = "conference"
    }
  }
}

# Pull-Secret für das private Image auf ghcr.io.
resource "kubernetes_secret_v1" "ghcr_pull" {
  metadata {
    name      = "ghcr-pull"
    namespace = kubernetes_namespace_v1.conference.metadata[0].name
  }
  type = "kubernetes.io/dockerconfigjson"
  data = {
    ".dockerconfigjson" = jsonencode({
      auths = {
        "ghcr.io" = {
          username = var.ghcr_username
          password = var.ghcr_token
          auth     = base64encode("${var.ghcr_username}:${var.ghcr_token}")
        }
      }
    })
  }
}

resource "helm_release" "argocd" {
  name             = "argocd"
  namespace        = "argocd"
  create_namespace = true
  repository       = "https://argoproj.github.io/argo-helm"
  chart            = "argo-cd"
  version          = var.argocd_chart_version
  wait             = true
  timeout          = 900

  values = [yamlencode({
    configs = {
      cm = {
        # Health von Child-Applications auswerten, damit Sync-Waves (CCM/CSI -> Operatoren -> App) aufeinander warten.
        "resource.customizations.health.argoproj.io_Application" = <<-LUA
          hs = {}
          hs.status = "Progressing"
          hs.message = ""
          if obj.status ~= nil and obj.status.health ~= nil then
            hs.status = obj.status.health.status
            if obj.status.health.message ~= nil then
              hs.message = obj.status.health.message
            end
          end
          return hs
        LUA
      }
    }
    # Kein öffentlicher Zugang: UI/CLI per "kubectl -n argocd port-forward svc/argocd-server 8080:443".
    server = {
      service = { type = "ClusterIP" }
    }
  })]

  depends_on = [kubernetes_secret_v1.hcloud]
}

# Root-Application (App of Apps): synchronisiert alle Applications unter deploy/argocd.
resource "helm_release" "root_app" {
  name       = "argocd-root"
  namespace  = "argocd"
  repository = "https://argoproj.github.io/argo-helm"
  chart      = "argocd-apps"
  version    = var.argocd_apps_chart_version

  values = [yamlencode({
    applications = {
      root = {
        namespace = "argocd"
        project   = "default"
        source = {
          repoURL        = var.repo_url
          targetRevision = var.repo_revision
          path           = "deploy/argocd"
        }
        destination = {
          server    = "https://kubernetes.default.svc"
          namespace = "argocd"
        }
        syncPolicy = {
          automated = { prune = true, selfHeal = true }
        }
      }
    }
  })]

  depends_on = [helm_release.argocd, kubernetes_secret_v1.ghcr_pull]
}
