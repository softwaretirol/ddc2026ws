# Deployment per ArgoCD (GitOps)

ArgoCD im Talos-Cluster (siehe [infra/hetzner-talos](../infra/hetzner-talos)) synchronisiert alles unter `deploy/` automatisch aus `main`. Die App ist unter **https://ddc2026.giesswein.dev** erreichbar. Die Entscheidungen dazu stehen im Plan [docs/plans/2026-11-23-argocd-deployment.md](../docs/plans/2026-11-23-argocd-deployment.md).

## Aufbau

| Pfad | Inhalt |
|---|---|
| `argocd/` | ArgoCD-Applications (App of Apps). Die Root-Application legt das Bootstrap an. |
| `platform/cluster-issuers/` | Let's-Encrypt-`ClusterIssuer` (staging und prod, HTTP-01 über Traefik) |
| `apps/conference/` | ConferenceApp (Deployment, Service, Ingress) und PostgreSQL (CloudNativePG) |

**Reihenfolge der Sync-Waves:**
1. `-3`: Hetzner CCM und CSI
2. `-2`: cert-manager, CloudNativePG, Traefik
3. `-1`: Issuer
4. `0`: App

## Erstinstallation (Bootstrap)

Voraussetzung: Der Cluster ist mit `infra/hetzner-talos/terraform` angelegt. Diese Dateien liegen dann lokal:
- `out/kubeconfig`
- `terraform.tfstate` des Cluster-Moduls

```powershell
cd infra/hetzner-talos/bootstrap
$env:TF_VAR_hcloud_token = (Get-Content ~\Desktop\Hetzner.txt -Raw).Trim()       # Hetzner-API-Token
$env:TF_VAR_ghcr_token   = (Get-Content ~\Desktop\GithubPAT.txt -Raw).Trim()      # Classic PAT, nur read:packages
terraform init
terraform apply
```

Das Bootstrap legt an:
- Secret `kube-system/hcloud` (Token und Netz-ID)
- Namespace `conference` mit Pull-Secret `ghcr-pull`
- ArgoCD
- die Root-Application

Die Tokens stehen nur im lokalen Terraform-State, nie in Git.

**DNS:** Ein A-Record `ddc2026.giesswein.dev` muss auf die öffentliche IP des Load Balancers `conference-ingress` zeigen. Die IP liefert `kubectl -n traefik get svc traefik`.

## Betrieb

- **ArgoCD-Oberfläche** (nicht öffentlich):
  ```powershell
  kubectl -n argocd port-forward svc/argocd-server 8080:443      # https://localhost:8080, Benutzer admin
  kubectl -n argocd get secret argocd-initial-admin-secret -o jsonpath='{.data.password}'   # base64
  ```
- **Neue App-Version:** In `apps/conference/kustomization.yaml` `newTag` auf den neuen Tag `sha-<commit>` setzen und auf `main` committen. ArgoCD rollt die Version aus.
- **Zertifikat:** Läuft mit `letsencrypt-prod`; cert-manager erneuert es automatisch. Zum Testen neuer Domains erst `letsencrypt-staging` verwenden (Annotation in `apps/conference/ingress.yaml`).
- **Datenbank:** `kubectl -n conference get cluster conference-db` zeigt den Zustand. Die Zugangsdaten liegen im Secret `conference-db-app`.

## Wichtig

- **Kein Zugriffsschutz:** Die App hat keine Anmeldung, und die API ist ohne Schutz öffentlich. Jeder kann Daten anlegen, ändern und löschen. Das ist bewusst entschieden (Plan, Entscheidung 2).
- **Volumes bleiben erhalten:** Die StorageClass `hcloud-volumes` nutzt `reclaimPolicy: Retain`. Nach dem Löschen eines PVC bleibt das Hetzner-Volume bestehen und kostet weiter. Bei Bedarf löschst du es in der Hetzner Console.
- **Keine Backups der Datenbank.** Sie gehören zu den späteren Ausbaustufen.
