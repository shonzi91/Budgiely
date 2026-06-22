# Deploy FinApp on Oracle Cloud (Always Free) — $0 forever

Runs the whole app in one container behind Caddy (free auto-HTTPS) on an Always-Free VM, with SQLite
on a persistent Docker volume. The image is **built in GitHub Actions and pulled from GHCR**, so even a
1 GB micro VM can run it (small VMs can't build the .NET/WASM image themselves). Total cost: **$0**.

```
Browser ──HTTPS──▶ Caddy (:443, auto Let's Encrypt) ──▶ FinApp container (:8080) ──▶ /data SQLite volume
                                                          ▲ image pulled from ghcr.io/shonzi91/finapp
```

## 0. One-time: make the GHCR image public
After the first push to `main`, the **Build and publish image** GitHub Action publishes
`ghcr.io/shonzi91/finapp`. By default a new GHCR package is **private**; make it public so the VM can
pull it without credentials:
- GitHub → your profile → **Packages → finapp → Package settings → Change visibility → Public**.

(Alternatively keep it private and `docker login ghcr.io` on the VM with a read-only PAT.)

## 1. Create the free VM
1. <https://cloud.oracle.com> → **Compute → Instances → Create instance**.
   - **Image:** Canonical **Ubuntu 24.04** *or* **Oracle Linux 8/9** (both supported by `setup.sh`).
   - **Shape:** the AMD **VM.Standard.E2.1.Micro** (1 GB) is fine since we only *pull* the image; the ARM
     **VM.Standard.A1.Flex** (more RAM) also works.
   - **SSH keys:** download the key pair.
   - Keep the default public subnet with a **public IPv4**.
2. Note the **public IP**. (Login user: `ubuntu` for Ubuntu images, `opc` for Oracle Linux.)

## 2. Open ports 80 + 443 in Oracle's cloud firewall
**Networking → Virtual Cloud Networks → (your VCN) → Security Lists → Default Security List → Add Ingress
Rules:** TCP **80** and TCP **443**, source `0.0.0.0/0`. (The OS firewall is handled by `setup.sh`.)

## 3. Point a free domain at the VM (needed for HTTPS)
At <https://www.duckdns.org>: sign in, create a subdomain (e.g. `finapp-you`), set its IP to your VM's
public IP. Your URL becomes `finapp-you.duckdns.org`. (Any domain with an `A` record to the VM works.)

## 4. Prep the host (Docker + swap + firewall)
SSH in (`ssh -i your-key ubuntu@<ip>` or `opc@<ip>`), then:
```bash
sudo dnf install -y git 2>/dev/null || sudo apt-get update -y && sudo apt-get install -y git
git clone https://github.com/shonzi91/FinApp.git
cd FinApp/deploy/oracle
chmod +x setup.sh && ./setup.sh        # installs Docker, adds swap on small VMs, opens 80/443
exit                                    # log out so your user joins the 'docker' group
```
SSH back in and confirm:
```bash
docker --version && docker compose version
```

## 5. Configure and launch
```bash
cd ~/FinApp/deploy/oracle
cp .env.example .env
sed -i "s|^JWT_KEY=.*|JWT_KEY=$(openssl rand -base64 48)|" .env
sed -i "s|^DOMAIN=.*|DOMAIN=finapp-you.duckdns.org|" .env   # <-- your domain
cat .env                                # sanity-check
docker compose up -d                    # pulls the GHCR image + Caddy, starts both
docker compose logs -f
```
Watch for `finapp` → `Now listening on: http://[::]:8080` + `Applying migration …`, and `caddy` →
`certificate obtained successfully`. Then open **https://finapp-you.duckdns.org** and create your account.

## Day-2 operations
| Task | Command (in `deploy/oracle`) |
| --- | --- |
| Update to latest | `git pull && docker compose pull && docker compose up -d` |
| Logs | `docker compose logs -f` |
| Stop / start | `docker compose down` / `docker compose up -d` |
| **Back up the DB** | `docker compose cp finapp:/data/finapp-server.db ./backup-$(date +%F).db` |
| Rotate JWT key | edit `.env`, `docker compose up -d` (invalidates logins) |

## Notes
- **One instance only** — SQLite is a single file; don't run replicas. Fine for personal/family use.
- The account snapshot is stored **plaintext** server-side (E2E encryption is a later hardening item).
- Keep the `caddy_data` volume — it holds your issued certificates.
- New code: push to `main` → Action rebuilds the image → on the VM `docker compose pull && up -d`.
