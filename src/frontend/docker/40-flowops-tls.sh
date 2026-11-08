#!/bin/sh
# OPS-01: generates a short-lived self-signed certificate for localhost on first start, unless one is mounted at
# /etc/nginx/certs (e.g. from mkcert). Generated at start-up, never baked into the image.
set -eu
dir=/etc/nginx/certs
if [ -s "$dir/tls.crt" ] && [ -s "$dir/tls.key" ]; then
    exit 0
fi
openssl req -x509 -newkey rsa:2048 -nodes -days 30 -subj "/CN=localhost" \
    -addext "subjectAltName=DNS:localhost,IP:127.0.0.1" \
    -keyout "$dir/tls.key" -out "$dir/tls.crt" 2>/dev/null
echo "40-flowops-tls.sh: generated a self-signed certificate for localhost"
