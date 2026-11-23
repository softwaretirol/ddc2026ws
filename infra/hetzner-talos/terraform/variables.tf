variable "cluster_name" {
  description = "Name des Clusters (Präfix für alle Hetzner-Ressourcen)."
  type        = string
  default     = "conference"
}

variable "location" {
  description = "Hetzner-Standort."
  type        = string
  default     = "hel1"
}

variable "network_zone" {
  description = "Netzwerkzone passend zum Standort (hel1, fsn1, nbg1 = eu-central)."
  type        = string
  default     = "eu-central"
}

variable "control_plane_server_type" {
  description = "Servertyp der Control Plane."
  type        = string
  default     = "cpx22"
}

variable "worker_server_type" {
  description = "Servertyp der Worker."
  type        = string
  default     = "cpx22"
}

variable "worker_count" {
  description = "Anzahl der Worker-Nodes."
  type        = number
  default     = 2

  validation {
    condition     = var.worker_count >= 1 && var.worker_count <= 20
    error_message = "worker_count muss zwischen 1 und 20 liegen."
  }
}

variable "talos_version" {
  description = "Talos-Version – muss zum Snapshot passen."
  type        = string
  default     = "v1.14.6"
}

variable "talos_image_id" {
  description = "ID des mit Packer erstellten Talos-Snapshots (Ausgabe von 'packer build')."
  type        = string
}

variable "admin_cidrs" {
  description = "Quell-Netze, die Kubernetes-API (6443) und Talos-API (50000) erreichen dürfen. Leer = eigene öffentliche IPv4 (wird beim Lauf ermittelt)."
  type        = list(string)
  default     = []
}

variable "out_dir" {
  description = "Verzeichnis für kubeconfig und talosconfig (enthält Geheimnisse, ist per .gitignore ausgeschlossen)."
  type        = string
  default     = "../out"
}
