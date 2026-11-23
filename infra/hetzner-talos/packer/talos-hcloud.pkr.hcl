# Erstellt einen Hetzner-Cloud-Snapshot mit Talos Linux.
# Ablauf: temporären Server im Rescue-Modus starten, Talos-Image aus der Image Factory auf die Disk
# schreiben, Snapshot anlegen, Server löschen.
#   $env:HCLOUD_TOKEN = (Get-Content ~\Desktop\Hetzner.txt -Raw).Trim()
#   packer init .; packer build .

packer {
  required_plugins {
    hcloud = {
      source  = "github.com/hetznercloud/hcloud"
      version = ">= 1.6.0"
    }
  }
}

variable "talos_version" {
  type    = string
  default = "v1.14.6"
}

# Schematic der Talos Image Factory ohne Erweiterungen (factory.talos.dev).
variable "talos_schematic" {
  type    = string
  default = "376567988ad370138ad8b2698212367b8edcb69b5fd68c80be1f2ec7d603b4ba"
}

variable "location" {
  type    = string
  default = "hel1"
}

# Nur für den Bau-Server; der Snapshot läuft danach auf jedem x86-Typ mit ausreichend Disk.
variable "build_server_type" {
  type    = string
  default = "cpx22"
}

locals {
  image_url = "https://factory.talos.dev/image/${var.talos_schematic}/${var.talos_version}/hcloud-amd64.raw.xz"
}

source "hcloud" "talos" {
  # Token kommt aus der Umgebungsvariable HCLOUD_TOKEN.
  rescue        = "linux64"
  image         = "debian-12"
  location      = var.location
  server_type   = var.build_server_type
  ssh_username  = "root"
  snapshot_name = "talos-${var.talos_version}-amd64"
  snapshot_labels = {
    os            = "talos"
    talos_version = var.talos_version
    arch          = "amd64"
    managed_by    = "packer"
  }
}

build {
  sources = ["source.hcloud.talos"]

  provisioner "shell" {
    inline = [
      "set -eux",
      "apt-get update -qq && apt-get install -y -qq wget xz-utils",
      "wget -q --tries=3 -O /tmp/talos.raw.xz '${local.image_url}'",
      "xz -d -c /tmp/talos.raw.xz | dd of=/dev/sda bs=4M conv=fsync status=none",
      "sync",
    ]
  }
}
