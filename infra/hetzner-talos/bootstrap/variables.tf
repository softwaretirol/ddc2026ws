variable "kubeconfig_path" {
  description = "kubeconfig des Clusters (von infra/hetzner-talos/terraform erzeugt)."
  type        = string
  default     = "../out/kubeconfig"
}

variable "cluster_state_path" {
  description = "Terraform-State des Cluster-Moduls (liefert die Netz-ID)."
  type        = string
  default     = "../terraform/terraform.tfstate"
}

variable "hcloud_token" {
  description = "Hetzner-Cloud-API-Token für CCM/CSI. Nur per Umgebungsvariable TF_VAR_hcloud_token setzen."
  type        = string
  sensitive   = true
}

variable "ghcr_username" {
  description = "GitHub-Benutzer, dem das Pull-Token gehört."
  type        = string
  default     = "softwaretirol"
}

variable "ghcr_token" {
  description = "GitHub Classic PAT nur mit read:packages. Nur per Umgebungsvariable TF_VAR_ghcr_token setzen."
  type        = string
  sensitive   = true
}

variable "repo_url" {
  description = "Git-Repository, aus dem ArgoCD synchronisiert."
  type        = string
  default     = "https://github.com/softwaretirol/ddc2026ws.git"
}

variable "repo_revision" {
  description = "Branch/Tag für ArgoCD."
  type        = string
  default     = "main"
}

variable "argocd_chart_version" {
  type    = string
  default = "10.18.2"
}

variable "argocd_apps_chart_version" {
  type    = string
  default = "2.0.8"
}
