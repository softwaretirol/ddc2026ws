output "argocd_access" {
  description = "Zugang zur ArgoCD-Oberfläche (nicht öffentlich)."
  value       = "kubectl -n argocd port-forward svc/argocd-server 8080:443  ->  https://localhost:8080, Benutzer admin, Passwort: kubectl -n argocd get secret argocd-initial-admin-secret -o jsonpath='{.data.password}' (base64)"
}
