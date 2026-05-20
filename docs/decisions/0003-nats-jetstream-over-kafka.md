# 0003 — Use NATS JetStream as the message bus, not Kafka

## Status
Accepted
Date: 2026-05-20

## Context
Event-driven microservices need a message bus that supports:
- Persistent streams (so a consumer crash doesn't lose events)
- Replay (so a new consumer can catch up from history)
- Acknowledgements (so we know an event was processed)
- Reasonable durability guarantees

The two leading open-source options at this scale are **Apache Kafka** and **NATS JetStream**.

Kafka is the industry-default for large-scale event streaming. It's also operationally heavy: ZooKeeper (or KRaft), broker tuning, partition planning, JVM ops, multi-GB memory footprint per broker even idle.

NATS JetStream is newer, written in Go, ships as a single binary, runs comfortably in tens of MB of RAM, and supports the same core abstractions (durable streams, consumers, acks, replay) with simpler operational semantics.

## Decision
Use **NATS JetStream** as the message bus. Run as a single binary in Docker (and later as a StatefulSet in k3s).

The .NET client (`NATS.Client`) has first-class JetStream support, which matters because most of our services will be C#.

## Consequences

**Positive**
- Single binary, ~10 MB RAM idle. Runs on a Raspberry Pi if it ever needs to.
- Operational concepts are easier to internalize than Kafka's (streams, consumers, subjects vs. topics, partitions, consumer groups, offsets).
- Faster path to "first event flowing" — the goal of Phase 4 — without spending a week on Kafka operations.
- Subject-based addressing (`transactions.created.user-123`) is more flexible than Kafka's flat-topic model.
- Built-in features: key-value store, object store, request-reply — covers needs that would otherwise require add-ons.

**Negative**
- Less industry mindshare than Kafka. Skills transfer less directly to a typical microservices job.
- Smaller ecosystem of connectors / sinks (Kafka has the larger Debezium + Kafka Connect ecosystem). Not relevant for our scope.
- If the project ever genuinely needs to scale past a single-node JetStream deployment, the migration to a NATS cluster (or to Kafka) is real work.

**Neutral**
- We will explicitly **not** learn Kafka on this project. That's a deliberate scope cut.

## Alternatives Considered

- **Apache Kafka.** Rejected. Operational overhead disproportionate to a one-user learning project; would consume weeks better spent on actual microservices patterns.
- **RabbitMQ.** Solid message broker but the AMQP model (queues + exchanges + bindings) is a different mental model from log-based event streaming, and replay is awkward. Doesn't teach the patterns we want.
- **Redpanda.** Kafka-API-compatible, single binary, lighter ops. Strong candidate. Rejected because NATS's subject model is a better teacher of event-driven design than yet-another-Kafka-flavor, and the .NET client story is excellent.
- **AWS SNS+SQS / Azure Service Bus / GCP Pub/Sub.** Rejected. Cloud-native messaging is a different learning topic; can revisit if/when deploying to a cloud changes the equation.
