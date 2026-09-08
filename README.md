# EdgePulse

> Open-source platform for industrial asset monitoring and anomaly detection. Collects telemetry from PLCs, sensors and thermal cameras, processes it at the edge with offline resilience, and syncs to Azure for dashboards, alerting and historical analysis.

[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Status](https://img.shields.io/badge/status-Phase%201%20done%2C%20Phase%202%20next-brightgreen.svg)](#roadmap)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![Azure](https://img.shields.io/badge/Azure-IoT%20Hub%20%7C%20AKS-0078D4.svg)](https://azure.microsoft.com/)

---

## The problem

Small and mid-size industrial companies are sitting on a goldmine of operational data — PLCs, sensors, thermal cameras, SCADA systems — but that data is **trapped on local panels**. No remote access, no intelligent alerting, no historical analysis. The only way to know a motor is overheating is when it fails.

Enterprise solutions (PTC ThingWorx, Siemens MindSphere) solve this, but cost **€50k–€200k/year in licenses** alone. Out of reach for the vast majority of SMEs.

**EdgePulse closes that gap**: an open-source, self-hostable platform that brings industrial-grade monitoring at a fraction of the cost, with a managed SaaS option starting at €200/month.

## Why this project exists

EdgePulse is a solo, hands-on build — every line of application and infrastructure code is
written by the author, no exceptions. The goal isn't just to ship a working platform: it's
to reach a level of real, defensible depth in Terraform/IaC, Kubernetes, Azure DevOps CI/CD,
Docker, and .NET/Azure backend integration that holds up under technical-interview scrutiny,
not just theory. Each build phase is scoped around infrastructure and skills the project
actually needs, with a clear definition-of-done — working, running, and explainable — before
moving to the next one.

## Architecture at a glance

```
┌─────────────┐    ┌─────────────────────────────┐    ┌─────────────────────────┐    ┌─────────┐
│   FIELD     │    │           EDGE              │    │         CLOUD           │    │ CLIENT  │
│             │    │   (Raspberry Pi / Industrial │    │         (Azure)         │    │         │
│ PLCs        │───▶│            PC)              │───▶│ IoT Hub                 │───▶│ Angular │
│ Thermal cam │    │                             │    │   ▼                     │    │ Dash    │
│ MQTT sensors│    │ Adapters ▶ Gateway          │    │ Functions ▶ TimescaleDB │    │         │
│ Simulator   │    │     ▶ Anomaly Engine        │    │        ▶ Blob (cold)   │    │ REST    │
│             │    │     ▶ SQLite buffer         │    │   ▶ API (AKS, Entra ID)│    │ Webhook │
│             │    │                             │    │   ▶ SignalR Hub         │    │         │
│             │    │                             │    │ API ─Service Bus─▶      │    │         │
│             │    │                             │    │     Insights (OpenAI)   │    │         │
└─────────────┘    └─────────────────────────────┘    └─────────────────────────┘    └─────────┘
                                offline-first             AMQP/MQTT over TLS
                                                    Monitor/Log Analytics + Backup across all of it
```

Three design principles drive every choice:

- **Offline-first** — the edge keeps working when internet drops. SQLite buffers locally, sync resumes with retry + deduplication when the link is back.
- **Protocol-agnostic** — every data source implements `IDeviceAdapter`. Adding a new protocol does not touch the gateway.
- **Cloud-agnostic core** — the edge layer has zero hard Azure dependency. It can run fully on-premise. Azure is the default, not a requirement.

## Architecture style

EdgePulse is a set of independently deployable services, not a monolith — edge (`Simulator`,
`Edge.Gateway`, `Edge.AnomalyEngine`) and cloud (`Cloud.Api`, `Cloud.Functions`,
`Cloud.Insights`, `Dashboard`), each with its own deploy target and, from Phase 4, its own
Helm chart on AKS. Inter-service communication follows the shape that fits each edge, not one
default: IoT Hub (device→cloud telemetry ingestion), Azure Functions (event-triggered
routing), one Azure Service Bus topic for async pub/sub between `Cloud.Api` and
`Cloud.Insights` (`AlertRaised` → `InsightGenerated`), and HTTP/SignalR only where a client
needs a synchronous request or a live push. See [`docs/BLUEPRINT.md`](docs/BLUEPRINT.md) for
the reasoning and scope caps on the async-messaging piece.

## Tech stack

| Layer       | Stack                                                                       |
|-------------|-----------------------------------------------------------------------------|
| Edge        | ASP.NET Core 8, Worker Services, ML.NET, SQLite, EF Core                    |
| Cloud       | Azure IoT Hub, Azure Functions, Azure Service Bus (async pub/sub), AKS, PostgreSQL + TimescaleDB, Blob Storage (cold archive), Key Vault |
| Identity & governance | Microsoft Entra ID (app registrations, RBAC), Azure Policy, Cost Management |
| Frontend    | Angular 18+ (standalone components, signals), SignalR client                |
| DevOps      | Docker, Helm, K3s (+ throwaway kubeadm lab), Terraform IaC (+ one Bicep exercise), GitHub Actions, Azure DevOps Pipelines/Boards, Argo CD (GitOps), Trivy |
| Observability & DR | Azure Monitor, Log Analytics, Recovery Services (backup/restore), Prometheus, Grafana |

## Roadmap

EdgePulse is built in four shippable phases — each one produces something demonstrable.

| Phase | Goal                          | Deliverables                                                                                  | Status     |
|-------|-------------------------------|-----------------------------------------------------------------------------------------------|------------|
| **1** | Data reaches the cloud        | Device Simulator, `IDeviceAdapter`, Modbus + MQTT adapters, Worker loop, SQLite buffer, IoT Hub forward, local Docker Compose | 🟢 Done — verified end-to-end via `docker-compose up` |
| **2** | Data is queryable and the cloud is governed | Azure Function routing, TimescaleDB schema, Blob cold archive, Backend API (Entra ID-secured), Cloud.Insights (Azure OpenAI alert explanations) with async pub/sub to the API via Service Bus, AKS deploy, Helm chart, Terraform base, Azure governance (RBAC/Policy/Cost), Monitor + backup/restore drill | ⚪ Planned  |
| **3** | The system is intelligent     | Anomaly Engine (rules + ML.NET), SignalR real-time, alert webhook/email, Angular dashboard     | ⚪ Planned  |
| **4** | The system is shippable       | Full Terraform IaC (+ one Bicep exercise), GitHub Actions + Azure DevOps Pipelines/Boards, Argo CD GitOps sync, multi-tenant isolation, self-managed K8s cluster admin drills, ADRs, security scanning, live demo deployment + walkthrough video | ⚪ Planned  |

## Repository layout

```
edgepulse/
├── src/
│   ├── EdgePulse.Simulator/          ← start here, day 1
│   ├── EdgePulse.Gateway/            ← Worker Service + adapters + IoT Hub forwarding
│   ├── EdgePulse.Edge.AnomalyEngine/ ← rules + ML.NET (Phase 3)
│   ├── EdgePulse.Cloud.Api/          ← ASP.NET Core API + SignalR (Phase 2)
│   ├── EdgePulse.Cloud.Functions/    ← Azure Functions (Phase 2)
│   └── EdgePulse.Dashboard/          ← Angular app (Phase 3)
├── tests/
│   └── EdgePulse.Gateway.Tests/      ← adapter, buffering, forwarder unit tests
├── mosquitto/config/                 ← local MQTT broker config for docker-compose
├── docker-compose.yml                ← local dev stack: mosquitto + simulator + gateway
├── infra/                            ← Terraform + Bicep templates (Phase 2)
├── k8s/                              ← Helm charts (Phase 2+)
├── docs/
│   └── adr/                          ← Architecture Decision Records
└── .github/workflows/                ← CI/CD pipelines (Phase 4)
```

## Quickstart (local dev)

**Prerequisites:** .NET 8 SDK, Docker Desktop, Git.

Run the full edge stack (Mosquitto broker + Simulator + Gateway) with Docker Compose:

```bash
git clone https://github.com/<your-user>/edgepulse.git
cd edgepulse

docker-compose up --build
```

This brings up Mosquitto, the Simulator, and the Gateway; the Gateway buffers readings to
SQLite and attempts to forward them to Azure IoT Hub (expected to fail/retry until IoT Hub is
provisioned in Phase 2).

Or run just the simulator directly with `dotnet`:

```bash
dotnet run --project src/EdgePulse.Simulator
```

The simulator emits synthetic telemetry (a simulated PLC over Modbus TCP, a humidity sensor
over MQTT) that the Gateway picks up, buffers to SQLite, and forwards to Azure IoT Hub once
a connection string is configured (`IotHubForwarder` in `appsettings.json` — IoT Hub itself
is provisioned in Phase 2/Terraform, not required for local dev).

## Architectural decisions

Key trade-offs are documented as ADRs in [`docs/adr/`](docs/adr/). Written so far:

- **ADR-0001** — Bounded `Channel<Reading>` with `BoundedChannelFullMode.Wait` for backpressure
- **ADR-0002** — Mark a reading forwarded only after IoT Hub acknowledges it (at-least-once delivery)
- **ADR-0003** — Migrate `ReadingRepository` from Dapper to EF Core

More get added as later phases land — candidates already identified include TimescaleDB vs.
InfluxDB/Azure Data Explorer, SQLite as edge buffer vs. a local MQTT broker, K3s on edge vs.
standalone Docker Compose, and ML.NET on-edge vs. Azure Cognitive Services. Numbers are
assigned in the order ADRs are actually written, not pre-reserved by topic.

## License

MIT — see [LICENSE](LICENSE).

## Author

Built by [@pelle04](https://github.com/pelle04) as an end-to-end portfolio project covering edge computing, cloud-native infrastructure, real-time streaming and machine learning on industrial data.
