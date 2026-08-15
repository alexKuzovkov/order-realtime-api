### [order-realtime-api](https://github.com/alexKuzovkov/order-realtime-api)

**Real-time .NET 9 backend** focused on horizontally scalable live updates and explicit order-state management.

- SignalR + Redis backplane
- Multi-instance runtime behind Nginx
- Explicit `OrderState`: Active / Completed / Inactive / Faulted
- Atomic terminal state transitions
- Idempotent order creation
- Background expiration
- Kubernetes HPA and health probes
- Docker-based E2E CI

`.NET 9` · `ASP.NET Core` · `SignalR` · `Redis` · `Nginx` · `Docker` · `Kubernetes`
