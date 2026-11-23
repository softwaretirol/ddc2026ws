output "control_plane" {
  description = "Control Plane (öffentliche und private IP)."
  value = {
    name       = hcloud_server.control_plane.name
    public_ip  = hcloud_server.control_plane.ipv4_address
    private_ip = local.control_plane_ip
  }
}

output "workers" {
  description = "Worker-Nodes (öffentliche und private IP)."
  value = [for i, s in hcloud_server.worker : {
    name       = s.name
    public_ip  = s.ipv4_address
    private_ip = local.worker_ips[i]
  }]
}

output "admin_cidrs" {
  description = "Netze, die Kubernetes- und Talos-API erreichen dürfen."
  value       = local.admin_cidrs
}

output "kubeconfig_path" {
  value = abspath(local_sensitive_file.kubeconfig.filename)
}

output "talosconfig_path" {
  value = abspath(local_sensitive_file.talosconfig.filename)
}
