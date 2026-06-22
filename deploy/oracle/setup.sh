#!/usr/bin/env bash
# One-time host prep for a fresh cloud VM. Works on Oracle Linux (dnf) and Ubuntu (apt).
# Installs Docker + git, adds swap on small VMs, and opens the OS firewall for HTTP/HTTPS.
# NOTE: Oracle's *cloud* firewall (VCN Security List / NSG) must also allow 80 + 443 ingress —
# that's done in the Oracle web console, see README.md.
set -euo pipefail

if command -v dnf >/dev/null 2>&1; then PKG=dnf
elif command -v apt-get >/dev/null 2>&1; then PKG=apt
else echo "Unsupported OS: need dnf or apt-get."; exit 1; fi
echo "==> Package manager: $PKG"

# A 1 GB micro VM thrashes without swap. Add 2 GB if RAM < ~2 GB and there's no swapfile yet.
mem_kb=$(awk '/MemTotal/{print $2}' /proc/meminfo)
if [ "$mem_kb" -lt 2000000 ] && [ ! -f /swapfile ]; then
  echo "==> Adding 2 GB swap…"
  sudo fallocate -l 2G /swapfile || sudo dd if=/dev/zero of=/swapfile bs=1M count=2048
  sudo chmod 600 /swapfile && sudo mkswap /swapfile && sudo swapon /swapfile
  echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab >/dev/null
fi

echo "==> Installing Docker + git…"
if [ "$PKG" = dnf ]; then
  sudo dnf install -y dnf-plugins-core git
  sudo curl -fsSL https://download.docker.com/linux/centos/docker-ce.repo -o /etc/yum.repos.d/docker-ce.repo
  sudo dnf install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin --allowerasing
  echo "==> Opening firewall (firewalld) for 80/443…"
  sudo firewall-cmd --permanent --add-port=80/tcp
  sudo firewall-cmd --permanent --add-port=443/tcp
  sudo firewall-cmd --reload
else
  sudo apt-get update -y
  sudo apt-get install -y ca-certificates curl git
  sudo install -m 0755 -d /etc/apt/keyrings
  curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
  sudo chmod a+r /etc/apt/keyrings/docker.gpg
  . /etc/os-release
  echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu ${VERSION_CODENAME} stable" \
    | sudo tee /etc/apt/sources.list.d/docker.list >/dev/null
  sudo apt-get update -y
  sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin
  echo "==> Opening firewall (iptables) for 80/443…"
  sudo iptables -I INPUT 1 -p tcp --dport 80  -j ACCEPT
  sudo iptables -I INPUT 1 -p tcp --dport 443 -j ACCEPT
  sudo apt-get install -y iptables-persistent >/dev/null 2>&1 || true
  sudo netfilter-persistent save || true
fi

sudo systemctl enable --now docker
sudo usermod -aG docker "$USER"

echo
echo "==> Done. LOG OUT and BACK IN (so your user joins the 'docker' group), then:"
echo "      cd ~/FinApp/deploy/oracle"
echo "      cp .env.example .env    # set DOMAIN + JWT_KEY"
echo "      docker compose up -d    # pulls the prebuilt image from GHCR"
