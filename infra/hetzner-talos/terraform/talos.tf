locals {
  control_plane_public_ip = hcloud_server.control_plane.ipv4_address
  # Intern sprechen Nodes die API über das private Netz an.
  cluster_endpoint = "https://${local.control_plane_ip}:6443"

  # Talos v1.14 nutzt eigenständige Konfigurationsdokumente (KubeNodeConfig, KubeletConfig,
  # UnattendedInstallConfig, ...). Was dort geregelt ist, darf nicht zusätzlich im alten v1alpha1-Format
  # (machine.kubelet, machine.install, cluster.apiServer) gepatcht werden.
  # Installations-Disk /dev/sda ist der Standard im UnattendedInstallConfig.

  # Kubelet bzw. Node-Adresse aus dem privaten Netz.
  common_patch = yamlencode({
    apiVersion = "v1alpha1"
    kind       = "KubeNodeConfig"
    nodeIP = {
      validSubnets = [local.subnet_cidr]
    }
  })

  # Kubelet mit --cloud-provider=external: der Hetzner Cloud Controller Manager (deploy/argocd/hcloud-ccm.yaml)
  # übernimmt providerID, Node-Adressen und Load-Balancer-Ziele.
  cloud_provider_patch = yamlencode({
    cluster = {
      externalCloudProvider = {
        enabled = true
      }
    }
  })

  # Talos-API-Zertifikat mit öffentlicher und privater IP; etcd kommuniziert über das private Netz.
  # Das kube-apiserver-Zertifikat enthält die Node-Adressen (inkl. öffentlicher IPv4) automatisch.
  control_plane_patch = yamlencode({
    machine = {
      certSANs = [local.control_plane_public_ip, local.control_plane_ip]
    }
    cluster = {
      etcd = {
        advertisedSubnets = [local.subnet_cidr]
      }
    }
  })
}

resource "talos_machine_secrets" "this" {
  talos_version = var.talos_version
}

data "talos_machine_configuration" "control_plane" {
  cluster_name     = var.cluster_name
  cluster_endpoint = local.cluster_endpoint
  machine_type     = "controlplane"
  machine_secrets  = talos_machine_secrets.this.machine_secrets
  talos_version    = var.talos_version
  config_patches   = [local.common_patch, local.cloud_provider_patch, local.control_plane_patch]
}

data "talos_machine_configuration" "worker" {
  cluster_name     = var.cluster_name
  cluster_endpoint = local.cluster_endpoint
  machine_type     = "worker"
  machine_secrets  = talos_machine_secrets.this.machine_secrets
  talos_version    = var.talos_version
  config_patches   = [local.common_patch, local.cloud_provider_patch]
}

data "talos_client_configuration" "this" {
  cluster_name         = var.cluster_name
  client_configuration = talos_machine_secrets.this.client_configuration
  endpoints            = [local.control_plane_public_ip]
  nodes                = [local.control_plane_public_ip]
}

# Konfiguration über die Talos-API (Port 50000) der öffentlichen IPs übertragen.
resource "talos_machine_configuration_apply" "control_plane" {
  client_configuration        = talos_machine_secrets.this.client_configuration
  machine_configuration_input = data.talos_machine_configuration.control_plane.machine_configuration
  node                        = local.control_plane_public_ip
  endpoint                    = local.control_plane_public_ip
}

resource "talos_machine_configuration_apply" "worker" {
  count                       = var.worker_count
  client_configuration        = talos_machine_secrets.this.client_configuration
  machine_configuration_input = data.talos_machine_configuration.worker.machine_configuration
  node                        = hcloud_server.worker[count.index].ipv4_address
  endpoint                    = hcloud_server.worker[count.index].ipv4_address
}

resource "talos_machine_bootstrap" "this" {
  client_configuration = talos_machine_secrets.this.client_configuration
  node                 = local.control_plane_public_ip
  endpoint             = local.control_plane_public_ip

  depends_on = [talos_machine_configuration_apply.control_plane]
}

resource "talos_cluster_kubeconfig" "this" {
  client_configuration = talos_machine_secrets.this.client_configuration
  node                 = local.control_plane_public_ip
  endpoint             = local.control_plane_public_ip

  depends_on = [talos_machine_bootstrap.this]
}

# kubeconfig zeigt für Admins auf die öffentliche IP (Zertifikat enthält sie als SAN).
resource "local_sensitive_file" "kubeconfig" {
  filename        = "${path.module}/${var.out_dir}/kubeconfig"
  file_permission = "0600"
  content = replace(
    talos_cluster_kubeconfig.this.kubeconfig_raw,
    local.cluster_endpoint,
    "https://${local.control_plane_public_ip}:6443"
  )
}

resource "local_sensitive_file" "talosconfig" {
  filename        = "${path.module}/${var.out_dir}/talosconfig"
  file_permission = "0600"
  content         = data.talos_client_configuration.this.talos_config
}
