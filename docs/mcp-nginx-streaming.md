# MCP Server over Nginx: Required Streaming Configuration

This project now exposes MCP tools over HTTP at the `/mcp` endpoint.

MCP is not a regular short request/response API flow. It relies on long-lived HTTP streaming behavior, so reverse-proxy defaults that are fine for normal REST traffic can break MCP sessions.

## Why this matters

If `/mcp` is routed through Nginx without streaming-safe settings, clients can see:

- stuck or delayed tool responses
- sessions closing unexpectedly
- partial output due to buffering or timeout behavior

For that reason, every Nginx hop must explicitly configure `/mcp` for streaming.

## Mandatory rule for multi-layer proxy setups

If you have more than one proxy layer, for example:

- public/edge Nginx -> internal Nginx in Docker Compose -> API service

then all layers that proxy `/mcp` must preserve streaming behavior.

Configuring only the inner layer is not enough. The outer layer can still buffer, downgrade, or timeout the stream.

## Required Nginx directives for `/mcp`

Use a dedicated `location /mcp` block and keep these directives:

```nginx
location /mcp {
    proxy_pass http://company_info_api/mcp;

    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;

    # Required for streamable HTTP transport
    proxy_http_version 1.1;
    proxy_set_header Connection "";

    # Do not buffer streaming responses
    proxy_buffering off;
    proxy_cache off;
    chunked_transfer_encoding off;

    # Keep long-running MCP sessions alive
    proxy_read_timeout 86400s;
    proxy_send_timeout 86400s;
}
```

## Current repository baseline

The VPS Nginx config already includes a dedicated `/mcp` location with the required streaming parameters.

- Nginx config file: `deploy/nginx.conf`
- Docker Compose entry point: `deploy/docker-compose-vps.yml` (Nginx is exposed on port 80)

This Nginx container is the public HTTP entry for the compose stack, so `/mcp` must remain configured there.

## If you have an additional reverse proxy

If traffic also passes through another Nginx instance before reaching this compose stack, add the same `/mcp` streaming directives there too.

A practical pattern:

1. Outer Nginx: `location /mcp` with streaming-safe directives, proxying to inner Nginx.
2. Inner Nginx (this repo): `location /mcp` with streaming-safe directives, proxying to API service.

Both layers must avoid buffering and support long-lived HTTP/1.1 streaming.

## Verification checklist

After any proxy change, validate:

- `/mcp` reaches the API through all proxy layers
- responses are streamed progressively (not delayed until request end)
- sessions remain stable for long-running tool calls
- no premature disconnects in Nginx logs

If MCP behavior regresses, inspect `/mcp` blocks in every proxy layer first.