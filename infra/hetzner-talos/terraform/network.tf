locals {
  subnet_cidr      = "10.0.1.0/24"
  control_plane_ip = "10.0.1.10"
  worker_ips       = [for i in range(var.worker_count) : cidrhost(local.subnet_cidr, 20 + i)]

  labels = {
    cluster    = var.cluster_name
    managed_by = "terraform"
  }
}

# Eigene öffentliche IPv4 als Standard für den Admin-Zugriff.
data "http" "my_ip" {
  url = "https://ipv4.icanhazip.com"
}

locals {
  admin_cidrs = length(var.admin_cidrs) > 0 ? var.admin_cidrs : ["${chomp(data.http.my_ip.response_body)}/32"]
}

resource "hcloud_network" "cluster" {
  name     = "${var.cluster_name}-net"
  ip_range = "10.0.0.0/16"
  labels   = local.labels
}

resource "hcloud_network_subnet" "nodes" {
  network_id   = hcloud_network.cluster.id
  type         = "cloud"
  network_zone = var.network_zone
  ip_range     = local.subnet_cidr
}

resource "hcloud_placement_group" "cluster" {
  name   = "${var.cluster_name}-spread"
  type   = "spread"
  labels = local.labels
}

# Öffentlich nur Kubernetes- und Talos-API, nur von den Admin-Netzen.
# Verkehr zwischen den Nodes läuft über das private Netz und ist davon nicht betroffen.
resource "hcloud_firewall" "cluster" {
  name   = "${var.cluster_name}-fw"
  labels = local.labels

  rule {
    description = "Kubernetes API"
    direction   = "in"
    protocol    = "tcp"
    port        = "6443"
    source_ips  = local.admin_cidrs
  }

  rule {
    description = "Talos API"
    direction   = "in"
    protocol    = "tcp"
    port        = "50000"
    source_ips  = local.admin_cidrs
  }

  rule {
    description = "ICMP"
    direction   = "in"
    protocol    = "icmp"
    source_ips  = local.admin_cidrs
  }
}
