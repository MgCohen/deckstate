# docker-hello

A tiny hello-world backend service for **exercising the Docker flow in a Claude Code web/cloud session** — a stand-in for a real backend so you can rehearse "build an image, run a container, hit its endpoints."

Standard-library Python only, so the image builds with no network access.

## Endpoints

| Method | Path        | Returns |
|--------|-------------|---------|
| GET    | `/`         | greeting + container hostname + timestamp |
| GET    | `/health`   | `{"status": "ok"}` (also the container HEALTHCHECK) |
| GET    | `/outbound` | result of an outbound HTTPS call — demonstrates the network modes |

## Try it in a fresh session — just prompt Claude

> **Prerequisite:** the environment's setup script starts the Docker daemon (see the Docker-on-web guide). Then:

- **Build & run it:**
  > "Build the image in `docker-hello/` and run it on port 8080, then curl `/` and `/health`."

- **See it with full internet** (the `/outbound` call should succeed):
  > "Run docker-hello **with internet access** and curl `/outbound`."

- **See it isolated** (the `/outbound` call should fail — proving isolation):
  > "Run docker-hello with **no network** and show me what `/outbound` returns."

## Run it by hand (reference)

```bash
# build
docker build -t docker-hello docker-hello/

# run with full internet (drun trusts the proxy CA -> /outbound succeeds)
drun --rm -d -p 8080:8080 --name hello docker-hello
curl -s localhost:8080/ ; curl -s localhost:8080/outbound

# run isolated (no egress -> /outbound reports unreachable)
docker run --rm -d -p 8080:8080 --network none --name hello docker-hello

# stop
docker rm -f hello
```
