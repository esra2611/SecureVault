# AI usage disclosure

This project may use AI-assisted development (e.g. Cursor, GitHub Copilot, or similar) for:

- **Implementation:** Boilerplate, CRUD, tests, and scaffolding aligned with the architecture in [architecture.md](./architecture.md).
- **Review:** Consistency with Clean Architecture, SOLID, OWASP, and the documented crypto/expiry/concurrency decisions.
- **Documentation:** README, architecture docs, and inline comments.

**Human responsibilities:**

- All architecture decisions, threat model, crypto strategy, and security controls are human-designed and documented in `docs/architecture.md`.
- Code and design are reviewed by maintainers; AI output is not accepted without verification against the architecture and security requirements.
- Final accountability for security, correctness, and compliance remains with the project maintainers.

**Architectural extensions (logged for traceability):**

- **Optional password protection (PBKDF2):** Optional password protection for secrets was implemented as an architectural extension: domain aggregate extended with `IsPasswordProtected`, `PasswordHash`, `PasswordSalt`; application layer uses PBKDF2 (Rfc2898DeriveBytes, SHA-256, ≥100k iterations) for key derivation and verification hash; constant-time comparison and unified error response to prevent enumeration; API and frontend support optional password on create and reveal. Documented in [architecture.md](./architecture.md) (§4 Crypto strategy, §8 Security requirements).

**References:** [architecture.md](./architecture.md), OWASP, NIST, and RFCs cited in the architecture document.
