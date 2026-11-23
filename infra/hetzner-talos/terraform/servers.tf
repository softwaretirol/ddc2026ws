# Die Server starten aus dem Talos-Snapshot im Wartungsmodus und warten auf ihre Maschinenkonfiguration
# (die überträgt talos.tf über die Talos-API).

resource "hcloud_server" "control_plane" {
  name               = "${var.cluster_name}-cp-1"
  image              = var.talos_image_id
  server_type        = var.control_plane_server_type
  location           = var.location
  placement_group_id = hcloud_placement_group.cluster.id
  firewall_ids       = [hcloud_firewall.cluster.id]
  labels             = merge(local.labels, { role = "control-plane" })

  public_net {
    ipv4_enabled = true
    ipv6_enabled = true
  }

  network {
    network_id = hcloud_network.cluster.id
    ip         = local.control_plane_ip
  }

  depends_on = [hcloud_network_subnet.nodes]

  lifecycle {
    # Ein neuer Snapshot soll bestehende Nodes nicht neu erstellen (Upgrades laufen über talosctl upgrade).
    ignore_changes = [image]
  }
}

resource "hcloud_server" "worker" {
  count              = var.worker_count
  name               = "${var.cluster_name}-worker-${count.index + 1}"
  image              = var.talos_image_id
  server_type        = var.worker_server_type
  location           = var.location
  placement_group_id = hcloud_placement_group.cluster.id
  firewall_ids       = [hcloud_firewall.cluster.id]
  labels             = merge(local.labels, { role = "worker" })

  public_net {
    ipv4_enabled = true
    ipv6_enabled = true
  }

  network {
    network_id = hcloud_network.cluster.id
    ip         = local.worker_ips[count.index]
  }

  depends_on = [hcloud_network_subnet.nodes]

  lifecycle {
    ignore_changes = [image]
  }
}
