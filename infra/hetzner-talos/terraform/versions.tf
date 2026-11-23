terraform {
  required_version = ">= 1.6"

  required_providers {
    hcloud = {
      source  = "hetznercloud/hcloud"
      version = "~> 1.48"
    }
    talos = {
      source  = "siderolabs/talos"
      version = ">= 0.7.0"
    }
    http = {
      source  = "hashicorp/http"
      version = "~> 3.4"
    }
    local = {
      source  = "hashicorp/local"
      version = "~> 2.5"
    }
  }
}

# Token aus der Umgebungsvariable HCLOUD_TOKEN – nie in Dateien schreiben.
provider "hcloud" {}

provider "talos" {}
