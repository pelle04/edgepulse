# EdgePulse

> Open-source platform for industrial asset monitoring and anomaly detection. Collects telemetry from PLCs, sensors and thermal cameras, processes it at the edge with offline resilience, and syncs to Azure for dashboards, alerting and historical analysis.

[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Status](https://img.shields.io/badge/status-WIP%20%E2%80%94%20Phase%201-orange.svg)](#roadmap)
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
│ PLCs        │───▶│            PC)              │───▶│ IoT Hub                 │───▶│ React   │
│ Thermal cam │    │                             │    │   ▼                     │    │ Dash    │
│ MQTT sensors│    │ Adapters ▶ Gateway          │    │ Functions ▶ TimescaleDB │    │         │
│ Simulator   │    │     ▶ Anomaly Engine        │    │        ▶ Blob (cold)   │    │ REST    │
│             │    │     ▶ SQLite buffer         │    │   ▶ API (AKS, Entra ID)│    │ Webhook │
│             │    │                             │    │   ▶ SignalR Hub         │    │         │
└─────────────┘    └─────────────────────────────┘    └─────────────────────────┘    └─────────┘
                                offline-first             AMQP/MQTT over TLS
                                                    Monitor/Log Analytics + Backup across all of it
```

Three design principles drive every choice:

- **Offline-first** — the edge keeps working when internet drops. SQLite buffers locally, sync resumes with retry + deduplication when the link is back.
- **Protocol-agnostic** — every data source implements `IDeviceAdapter`. Adding a new protocol does not touch the gateway.
- **Cloud-agnostic core** — the edge layer has zero hard Azure dependency. It can run fully on-premise. Azure is the default, not a requirement.

## Tech stack

| Layer       | Stack                                                                       |
|-------------|-----------------------------------------------------------------------------|
| Edge        | ASP.NET Core 8, Worker Services, ML.NET, SQLite, Dapper                     |
| Cloud       | Azure IoT Hub, Azure Functions, AKS, PostgreSQL + TimescaleDB, Blob Storage (cold archive), Key Vault |
| Identity & governance | Microsoft Entra ID (app registrations, RBAC), Azure Policy, Cost Management |
| Frontend    | Angular 18+ (standalone components, signals), SignalR client                |
| DevOps      | Docker, Helm, K3s (+ throwaway kubeadm lab), Terraform IaC (+ one Bicep exercise), GitHub Actions, Azure DevOps Pipelines/Boards, Argo CD (GitOps), Trivy |
| Observability & DR | Azure Monitor, Log Analytics, Recovery Services (backup/restore), Prometheus, Grafana |

## Roadmap

EdgePulse is built in four shippable phases — each one produces something demonstrable.

| Phase | Goal                          | Deliverables                                                                                  | Status     |
|-------|-------------------------------|-----------------------------------------------------------------------------------------------|------------|
| **1** | Data reaches the cloud        | Device Simulator, `IDeviceAdapter`, Modbus + MQTT adapters, Worker loop, SQLite buffer, IoT Hub forward, local Docker Compose | 🟡 In progress |
| **2** | Data is queryable and the cloud is governed | Azure Function routing, TimescaleDB schema, Blob cold archive, Backend API (Entra ID-secured), AKS deploy, Helm chart, Terraform base, Azure governance (RBAC/Policy/Cost), Monitor + backup/restore drill | ⚪ Planned  |
| **3** | The system is intelligent     | Anomaly Engine (rules + ML.NET), SignalR real-time, alert webhook/email, Angular dashboard     | ⚪ Planned  |
| **4** | The system is shippable       | Full Terraform IaC (+ one Bicep exercise), GitHub Actions + Azure DevOps Pipelines/Boards, Argo CD GitOps sync, multi-tenant isolation, self-managed K8s cluster admin drills, ADRs, security scanning, live demo deployment + walkthrough video | ⚪ Planned  |

## Repository layout

```
edgepulse/
├── src/
│   ├── EdgePulse.Simulator/          ← start here, day 1
│   ├── EdgePulse.Edge.Gateway/       ← Worker Service + adapters
│   ├── EdgePulse.Edge.AnomalyEngine/ ← rules + ML.NET
│   ├── EdgePulse.Cloud.Api/          ← ASP.NET Core API + SignalR
│   ├── EdgePulse.Cloud.Functions/    ← Azure Functions
│   └── EdgePulse.Dashboard/          ← Angular app
├── infra/                            ← Bicep templates
├── k8s/                              ← Helm charts
├── edge-deployment/                  ← IoT Edge manifest + docker-compose
├── docs/
│   └── adr/                          ← Architecture Decision Records
└── .github/workflows/                ← CI/CD pipelines
```

## Quickstart (local dev)

> Phase 1 is in active development. Right now only the simulator is runnable.

**Prerequisites:** .NET 8 SDK, Docker Desktop, Git.

```bash
git clone https://github.com/<your-user>/edgepulse.git
cd edgepulse

# Run the device simulator
dotnet run --project src/EdgePulse.Simulator
```

The simulator will start emitting synthetic telemetry for three asset types (production line, electric motor, thermal camera) at 1 Hz, with controlled anomaly injection every N minutes.

## Architectural decisions

Key trade-offs are documented as ADRs in [`docs/adr/`](docs/adr/). Highlights:

- **ADR-001** — TimescaleDB over InfluxDB / Azure Data Explorer
- **ADR-002** — SQLite as edge buffer instead of a local MQTT broker
- **ADR-003** — K3s on edge instead of standalone Docker Compose
- **ADR-004** — ML.NET on-edge instead of Azure Cognitive Services

## License

MIT — see [LICENSE](LICENSE).

## Author

Built by [@pelle04](https://github.com/pelle04) as an end-to-end portfolio project covering edge computing, cloud-native infrastructure, real-time streaming and machine learning on industrial data.
