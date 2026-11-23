# Plan: ConferenceApp per ArgoCD auf dem Talos-Cluster, erreichbar unter https://ddc2026.giesswein.dev

- **Datum:** 2026-11-23
- **Status:** Freigegeben, in Umsetzung

**Entscheidungen vom 23.11.2026:**
1. **DNS:** Der Nutzer stellt `ddc2026.giesswein.dev` manuell in der Hetzner DNS Console auf die IP des Load Balancers um.
2. **Schreibzugriff:** **kein Schutz.** Das ist bewusst akzeptiert: Jeder kann über die öffentliche URL Daten anlegen, ändern und löschen. Die Traefik-Middleware entfällt.
3. **Image:** Pull-Secret `ghcr-pull` im Namespace `conference`. Grundlage ist ein Classic PAT, der nur `read:packages` darf. Terraform liest ihn aus einer lokalen Datei, er steht nie in Git.
4. **Daten:** Nach dem Deployment wird das DDC-2026-Programm (Sessionize) über die API importiert.

## 1. Ziel

ArgoCD läuft im Hetzner-Talos-Cluster und hält alles weitere per GitOps aus diesem Repo synchron:
- Ingress
- TLS-Zertifikate
- PostgreSQL über CloudNativePG
- die ConferenceApp aus `ghcr.io/softwaretirol/ddc2026ws`

Die App ist unter **https://ddc2026.giesswein.dev** mit gültigem Let's-Encrypt-Zertifikat erreichbar.

## 2. Ausgangslage und offene Entscheidungen

Geprüft am 23.11.2026:

| Fakt | Folge |
|---|---|
| Das Repo `softwaretirol/ddc2026ws` ist **öffentlich**. | ArgoCD liest es ohne Zugangsdaten. |
| Im Cluster fehlen Hetzner CCM und CSI. | Ohne sie gibt es keine Services vom Typ `LoadBalancer` und keine Persistent Volumes. Postgres braucht aber Volumes, und der Ingress braucht eine öffentliche IP. **CCM und CSI sind deshalb Voraussetzung.** |
| Die DNS-Zone `giesswein.dev` liegt bei Hetzner, aber **nicht in diesem Cloud-Projekt**. | Der vorhandene Token kann den DNS-Eintrag nicht setzen. |
| `ddc2026.giesswein.dev` existiert bereits als **A-Record auf `116.203.167.185`**, einen Server außerhalb dieses Projekts. | Das Umstellen auf den neuen Load Balancer würde das bisherige Ziel ablösen. |
| Das Paket auf ghcr.io ist vermutlich **privat**, so ist es nach dem ersten Push voreingestellt. | Der Cluster kann das Image ohne Pull-Secret nicht laden. |
| **Die App hat keine Anmeldung.** | Öffentlich erreichbar könnte jeder Konferenzen anlegen, ändern und löschen. |

**Offene Entscheidungen:**
1. **DNS:** Darf `ddc2026.giesswein.dev` auf die IP des neuen Load Balancers umgestellt werden? Wer stellt ihn um? Möglich ist manuell in der Hetzner DNS Console, oder ein DNS-API-Token für die Zone wird bereitgestellt.
2. **Schreibzugriff schützen:** Basic Auth vor allen schreibenden API-Aufrufen (`POST`, `PUT`, `DELETE`) per Traefik-Middleware (empfohlen), eine IP-Allowlist oder kein Schutz.
3. **Image:** Das ghcr-Paket öffentlich machen (empfohlen, das Repo ist ohnehin öffentlich) oder ein Pull-Secret mit Token (`read:packages`) im Cluster anlegen.
4. **Daten:** Die App spielt beim ersten Start die Demo-Konferenz ein. Soll danach zusätzlich das DDC-2026-Programm importiert werden?

## 3. Architektur

```
Internet ──443/80──► Hetzner Load Balancer lb11 (hel1)
                          │ privates Netz 10.0.1.0/24
                          ▼
                     Traefik (Ingress-Controller, Service type=LoadBalancer via hcloud-CCM)
                          │  Ingress ddc2026.giesswein.dev, TLS von cert-manager (Let's Encrypt, HTTP-01)
                          ▼
                     conference-app (Deployment, Image ghcr.io/softwaretirol/ddc2026ws:sha-…)
                          │  ConnectionStrings__conferencedb aus dem CNPG-Secret
                          ▼
                     CloudNativePG-Cluster "conference-db" (PostgreSQL, Volume über hcloud-CSI)
```

**Ebene 1: Bootstrap mit Terraform.** Neues Root-Modul `infra/hetzner-talos/bootstrap`. Es nutzt die Helm- und Kubernetes-Provider mit der `kubeconfig` aus `out/`:
- Secret `kube-system/hcloud` mit Token und Netz-ID. Das ist der einzige Ort für den Token im Cluster, er kommt per Umgebungsvariable und steht nie in Git.
- Basic-Auth-Secret für die App (falls Entscheidung 2 = Basic Auth). Das Passwort erzeugt Terraform und gibt es als sensitiven Output aus.
- ArgoCD per Helm, die Oberfläche nur per `kubectl port-forward` erreichbar und nicht öffentlich.
- Eine Root-Application nach dem Muster „App of Apps“, Quelle `deploy/argocd` in diesem Repo, Branch `main`.

**Ebene 2: GitOps unter `deploy/`.** Diesen Teil synchronisiert ArgoCD automatisch, mit Prune und Self-Heal. Die Reihenfolge steuern Sync-Waves:

| Wave | Application | Inhalt |
|---|---|---|
| -3 | `hcloud-ccm` | Helm-Chart `hcloud-cloud-controller-manager` mit `networking.enabled`, weil Nodes und Load Balancer im privaten Netz liegen |
| -3 | `hcloud-csi` | Helm-Chart `hcloud-csi`, StorageClass `hcloud-volumes` als Standard |
| -2 | `cert-manager` | Helm, inklusive CRDs |
| -2 | `cloudnative-pg` | Helm, Operator |
| -2 | `traefik` | Helm. Service vom Typ LoadBalancer mit den Annotationen Standort `hel1`, Typ `lb11`, `use-private-ip: true` und Name `conference-ingress`. Weiterleitung von HTTP auf HTTPS. |
| -1 | `cluster-issuers` | `ClusterIssuer` `letsencrypt-staging` und `letsencrypt-prod` (ACME HTTP-01 über Traefik) |
| 0 | `conference` | Kustomize, siehe unten |

**ConferenceApp** (`deploy/apps/conference/`, Namespace `conference`):
- **Datenbank:** CNPG-`Cluster` `conference-db` mit **2 Instanzen**, verteilt auf beide Worker (Primary und Replica). Je 10 Gi auf `hcloud-volumes`, Datenbank `conferencedb`, Owner `conference`.
- **`Deployment`:**
  - **1 Replica:** Die App wendet beim Start EF-Migrationen an, mehrere Replikas könnten sich dabei in die Quere kommen.
  - Image fest auf `sha-<commit>` gesetzt.
  - Ressourcen-Requests und -Limits.
  - Readiness- und Liveness-Probe per HTTP auf `/`.
- **Connection-String:** Er wird aus dem CNPG-Secret `conference-db-app` zusammengesetzt, über Umgebungsvariablen (`DB_USER`, `DB_PASSWORD`) und Kubernetes-Expansion `$(DB_PASSWORD)`. Host ist `conference-db-rw`.
- **`Service`** und **`Ingress`** (`ingressClassName: traefik`, Host `ddc2026.giesswein.dev`, TLS-Secret per cert-manager-Annotation).
- Bei Entscheidung 2 = Basic Auth: eine Traefik-`Middleware` (`basicAuth`) auf einer eigenen Route für `/api` mit schreibenden Methoden.

**Talos-Anpassung (Ebene 0, bestehendes Terraform):** `cluster.externalCloudProvider` aktivieren, damit der CCM die Nodes übernimmt (`providerID`, Load-Balancer-Ziele). Bei Talos v1.14 prüfe ich das Dokumentformat vorher mit `talosctl validate`.

## 4. Nicht im Umfang

- Anmeldung in der App selbst: Basic Auth ist nur eine Überbrückung bis zur Anmeldung
- Öffentliche ArgoCD-Oberfläche, SSO für ArgoCD
- Backups von Postgres in Object Storage (Barman/S3), Monitoring, Alerting
- Automatisches Aktualisieren des Image-Tags, siehe spätere Ausbaustufen
- Mehrere Umgebungen (Staging/Production)

## 5. Kosten

Zusätzlich zum Cluster (rund 49,50 € pro Monat):
- Load Balancer `lb11`: **6,99 € pro Monat**
- 2 Volumes à 10 GB: rund 1 € pro Monat

Zusammen **rund 57,50 € pro Monat**.

## 6. Umsetzungsschritte

1. [ ] Talos: External Cloud Provider aktivieren (Patch prüfen und anwenden, Nodes bleiben erhalten)
2. [ ] `deploy/`-Struktur mit allen ArgoCD-Applications und Manifests. Helm-Chart-Versionen fest gepinnt.
3. [ ] Bootstrap-Terraform: Secrets, ArgoCD, Root-Application
4. [ ] Commit nach `main` (Security-Hook), weil ArgoCD von `main` liest
5. [ ] `terraform apply` (bootstrap). Warten, bis alle Applications `Synced/Healthy` sind und der Load Balancer eine IP hat.
6. [ ] **DNS:** A-Record `ddc2026.giesswein.dev` auf die IP des Load Balancers (siehe Entscheidung 1)
7. [ ] Zertifikat zuerst über `letsencrypt-staging`, danach auf `letsencrypt-prod` umstellen
8. [ ] Prüfen:
   - `curl -I https://ddc2026.giesswein.dev` (gültiges Zertifikat, HTTP wird auf HTTPS weitergeleitet)
   - Agenda im Browser per Playwright
   - Schreibzugriff ohne Zugangsdaten liefert 401
   - CNPG-Cluster mit 2 Instanzen gesund
9. [ ] Optional: DDC-Programm importieren (Entscheidung 4)
10. [ ] README `deploy/README.md`: Betrieb, Updates, Zugang zu ArgoCD, Abbau

## 7. Annahmen

- Der Load Balancer erreicht die Nodes über das private Netz. Die bestehende Firewall (6443/50000 nur Admin-IP) bleibt deshalb unverändert, Ports 80/443 öffnet nur der Load Balancer.
- Let's Encrypt erreicht `http://ddc2026.giesswein.dev/.well-known/acme-challenge/…`, sobald der DNS-Eintrag umgestellt ist. Die DNS-TTL kann die Umstellung verzögern.
- Der Image-Tag wird zunächst von Hand in `deploy/apps/conference/kustomization.yaml` gesetzt, ausgehend von `sha-4c4859f`.
- Die Demo-Daten beim ersten Start sind für diese Umgebung in Ordnung.

## 8. Spätere Ausbaustufen

- **Image-Tag automatisch aktualisieren:** Der Container-Workflow schreibt den neuen `sha-`-Tag nach `deploy/` und committet, oder der ArgoCD Image Updater übernimmt das.
- **Anmeldung in der App** (OIDC), danach entfällt die Basic Auth.
- **CNPG-Backups** nach Hetzner Object Storage, `ScheduledBackup`.
- **Monitoring:** kube-prometheus-stack.
- **Secrets per SOPS/External Secrets** statt Bootstrap-Terraform.
