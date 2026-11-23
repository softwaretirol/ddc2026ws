# Kubernetes auf Hetzner Cloud mit Talos

Hier liegt die Infrastruktur für einen Cluster aus **1 Control Plane und 2 Workern** (`cpx22`, Helsinki). Die Entscheidungen dazu stehen in [docs/plans/2026-11-23-hetzner-talos-cluster.md](../../docs/plans/2026-11-23-hetzner-talos-cluster.md).

**Kosten:** 3 × `cpx22` (2 vCPU, 4 GB RAM, 80 GB Disk) je 16,49 € brutto pro Monat, also rund **49,50 € pro Monat**. Dazu kommen ein paar Cent für den Snapshot. Abgerechnet wird stündlich, solange die Server existieren.

## Voraussetzungen

- Terraform ≥ 1.6, Packer ≥ 1.11, talosctl, kubectl (zum Beispiel per `winget install Hashicorp.Terraform Hashicorp.Packer Sidero.talosctl`)
- Hetzner-Cloud-API-Token mit Lese- und Schreibrechten. Er kommt **nur** über die Umgebungsvariable `HCLOUD_TOKEN`, nie in eine Datei im Repo.

```powershell
$env:HCLOUD_TOKEN = (Get-Content ~\Desktop\Hetzner.txt -Raw).Trim()
```

## 1. Talos-Snapshot bauen (einmalig je Talos-Version)

Hetzner bietet Talos nicht als Image an. Packer startet deshalb einen temporären `cpx22`-Server im Rescue-Modus, schreibt das Image der Talos Image Factory auf die Disk, legt einen Snapshot an und löscht den Server wieder.

```powershell
cd infra/hetzner-talos/packer
packer init .
packer build .        # am Ende: "A snapshot was created: 'talos-v1.14.6-amd64' (ID: 123456789)"
```

## 2. Cluster anlegen

```powershell
cd infra/hetzner-talos/terraform
terraform init
terraform plan  '-var=talos_image_id=123456789'
terraform apply '-var=talos_image_id=123456789'
```

Die Argumente stehen in Anführungszeichen, weil Windows PowerShell 5.1 sie sonst am `.` oder `=` zerlegt.

- **Firewall:** Kubernetes-API (6443) und Talos-API (50000) sind nur von deiner aktuellen öffentlichen IPv4 aus erreichbar. Weitere Netze gibst du über `'-var=admin_cidrs=["203.0.113.0/24"]'` frei.
- **Ergebnis:** `infra/hetzner-talos/out/kubeconfig` und `out/talosconfig`. Sie sind per `.gitignore` ausgeschlossen, enthalten Zugangsdaten und dürfen nicht weitergegeben werden.

```powershell
$env:KUBECONFIG = "$PWD\..\out\kubeconfig"
kubectl get nodes -o wide
talosctl --talosconfig ..\out\talosconfig health
```

## 3. Abbauen

```powershell
terraform destroy '-var=talos_image_id=123456789'
```

Den Snapshot löscht `destroy` nicht, weil Packer ihn angelegt hat. Entferne ihn in der Hetzner Console unter *Images → Snapshots*.

## Hinweise

- **State:** `terraform.tfstate` liegt lokal und enthält die Talos-Secrets. Gib die Datei nicht weiter und sichere sie.
- **Upgrades:** Talos- und Kubernetes-Upgrades laufen über `talosctl upgrade` bzw. `talosctl upgrade-k8s`, nicht über einen neuen Snapshot. Änderungen am Image ignoriert Terraform.
- **Noch nicht enthalten:** Hetzner CCM und CSI. Services vom Typ `LoadBalancer` und Persistent Volumes funktionieren deshalb noch nicht. Die Ausbaustufen stehen im Plan.
