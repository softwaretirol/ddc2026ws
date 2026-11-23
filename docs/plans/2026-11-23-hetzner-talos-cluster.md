# Plan: Kubernetes-Cluster mit Talos bei Hetzner Cloud (Terraform)

- **Datum:** 2026-11-23
- **Status:** Umgesetzt. Der Cluster läuft seit 23.11.2026, alle 3 Nodes `Ready`, `talosctl health` OK.

> **Abweichung bei der Umsetzung:** Talos v1.14 erzeugt eigenständige Konfigurationsdokumente (`KubeNodeConfig`, `UnattendedInstallConfig`, `KubeAPIServerConfig` usw.). Der erste `apply` scheiterte deshalb, weil Werte doppelt im alten Format `machine.kubelet`, `machine.install` und `cluster.apiServer` gepatcht waren.
>
> Die Korrektur:
> - Die Node-IP steht jetzt im Dokument `KubeNodeConfig`.
> - Den Patch für die Installations-Disk braucht es nicht mehr, `/dev/sda` ist Standard.
> - API-Server-SANs gibt es nicht mehr als Patch. Das Zertifikat enthält die Node-Adressen automatisch, `kubectl` über die öffentliche IP ist geprüft.
>
> Servertyp `cpx22` statt `cx33`, weil `cx33` nicht verfügbar war. Talos-Snapshot: ID `460040319`.

## 1. Ziel

Mit Terraform entsteht in der Hetzner Cloud ein Kubernetes-Cluster auf Talos Linux: **1 Control Plane und 2 Worker**. Am Ende liegen `kubeconfig` und `talosconfig` lokal bereit. Später soll darauf die ConferenceApp laufen, mit dem Image `ghcr.io/softwaretirol/ddc2026ws`.

## 2. Anforderungen und Entscheidungen

| Thema | Entscheidung |
|---|---|
| Distribution | Talos Linux **v1.14.6**, Standard-Image der Talos Image Factory ohne Erweiterungen |
| Standort | **Helsinki (`hel1`)**, Netzwerkzone `eu-central` |
| Servertyp | **`cpx22`**: 2 vCPU (x86, shared), 4 GB RAM, 80 GB Disk. 16,49 € brutto pro Monat je Server laut Hetzner-API, also rund **49,50 € pro Monat** für 3 Nodes. Geändert, weil `cx33` nicht verfügbar war. Auch der Packer-Bauserver nutzt `cpx22`. |
| Architektur | x86, weil das App-Image nur für amd64 gebaut wird |
| Talos-Image | Hetzner bietet Talos nicht an. **Packer** startet deshalb einen temporären Server im Rescue-Modus, schreibt das Image auf die Disk und legt einen **Snapshot** an. Labels: `os=talos`, `talos_version=v1.14.6` |
| Netz | Privates Netz `10.0.0.0/16`, Subnetz `10.0.1.0/24`, feste IPs: Control Plane `10.0.1.10`, Worker `10.0.1.20` und `10.0.1.21`. Kubelet und etcd nutzen das private Netz. |
| Cluster-Endpunkt | Intern `https://10.0.1.10:6443`. Das Zertifikat enthält zusätzlich die öffentliche IP der Control Plane. Die `kubeconfig` für Admins zeigt auf die öffentliche IP. |
| Firewall | Eingehend nur **6443** (Kubernetes-API) und **50000** (Talos-API), nur von der eigenen öffentlichen IP. Terraform ermittelt sie beim Lauf, sie lässt sich per Variable `admin_cidrs` überschreiben. Verkehr im privaten Netz ist nicht betroffen. |
| Verteilung | Placement Group vom Typ `spread`, damit die Nodes auf verschiedenen Hosts laufen |
| Secrets | Der API-Token kommt nur per Umgebungsvariable `HCLOUD_TOKEN`, gelesen aus der lokalen Datei. Terraform-State, `kubeconfig` und `talosconfig` enthalten Geheimnisse und stehen in `.gitignore`. |

## 3. Nicht im Umfang

- Hetzner Cloud Controller Manager, also Services vom Typ `LoadBalancer`, und das CSI-Volume-Plugin
- Ingress, TLS, DNS
- Deployment der ConferenceApp und der Datenbank in den Cluster (eigener Folgeplan)
- Hochverfügbarkeit: Mit 1 Control Plane gibt es keine HA für etcd und die API
- Remote-State-Backend: Der State liegt lokal

## 4. Technologie und Aufbau

```
infra/hetzner-talos/
  packer/talos-hcloud.pkr.hcl   Snapshot aus der Talos Image Factory (hcloud-amd64.raw.xz)
  terraform/
    versions.tf                 Provider: hetznercloud/hcloud, siderolabs/talos, hashicorp/http, hashicorp/local
    variables.tf                Standort, Servertyp, Talos-Version, Snapshot-ID, admin_cidrs
    network.tf                  privates Netz, Firewall, Placement Group
    servers.tf                  Control Plane und 2 Worker aus dem Talos-Snapshot
    talos.tf                    Secrets, Maschinenkonfiguration, Apply, Bootstrap, kubeconfig/talosconfig
    outputs.tf                  IPs, Pfade der Konfigurationsdateien
```

Ablauf:
1. `packer build` erstellt den Snapshot und liefert dessen ID.
2. `terraform apply -var talos_image_id=<ID>` legt Netz, Firewall und Server an.
3. Die Server starten im Talos-Wartungsmodus.
4. Terraform überträgt die Maschinenkonfigurationen über die Talos-API und bootstrappt etcd auf der Control Plane.
5. Terraform schreibt `out/kubeconfig` und `out/talosconfig`.

## 5. Umsetzungsschritte

- [x] Werkzeuge installieren (winget): Terraform 1.17.2, Packer 1.16.2, talosctl 1.14.6
- [x] Servertypen und Preise für `hel1` über die Hetzner-API prüfen
- [x] Packer-Template und Terraform-Code schreiben, `.gitignore` erweitern
- [x] `packer validate`, `terraform init`, `terraform validate`
- [x] `terraform plan` mit Platzhalter-Snapshot vorstellen
- [x] **Freigabe abwarten.** Erst dann `packer build` (temporärer Server für einige Minuten, danach ein Snapshot) und `terraform apply`
- [x] Prüfen: `kubectl get nodes` (3 Nodes `Ready`), `talosctl health`
- [x] README im Ordner `infra/hetzner-talos` mit Bedienung und Abbau (`terraform destroy`, Snapshot löschen)
- [x] Commit (Security-Hook) und PR

## 6. Annahmen

- Die Hetzner-Servertypen in `hel1` sind verfügbar, Stand der API-Abfrage vom 23.11.2026.
- Die Talos Image Factory liefert `hcloud-amd64.raw.xz` für v1.14.6. Schematic ohne Erweiterungen: `376567988ad370138ad8b2698212367b8edcb69b5fd68c80be1f2ec7d603b4ba`.
- Als Netzwerk-Plugin genügt das Standard-CNI von Talos (Flannel).
- Die eigene öffentliche IP bleibt während des Betriebs stabil. Sonst `admin_cidrs` anpassen und `terraform apply` erneut ausführen.

## 7. Spätere Ausbaustufen

- Hetzner CCM und CSI, Ingress-Controller (Traefik oder NGINX), cert-manager
- Deployment der ConferenceApp mit PostgreSQL (CloudNativePG) und Helm-Chart bzw. Kustomize
- 3 Control Planes für HA, Load Balancer vor der API
- Remote State mit Verschlüsselung, Pipeline für Terraform
