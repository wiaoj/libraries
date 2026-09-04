# Evidence-Based Verification and Quality Assurance Standard

This rule governs all AI agents and contributors implementing bug fixes, concurrency mechanisms, distributed consensus, and test verification in `wiaoj/libraries`.

## 1. Mandatory "Red-to-Green" Verification
- Any reported bug fix or edge-case remediation must provide evidence that the test failed under the bug condition and passed after the fix.
- "Tests are green" without establishing that the test actually reproduced the failure mode is insufficient.

## 2. Concurrency & Race-Condition Stress Testing (N >= 50)
- Concurrency, lease expiration, distributed locks, and multithreaded queue tests cannot rely on a single execution pass.
- Concurrency regression tests must be executed in a repetition loop (at least 50 iterations) with a 100% pass rate before claiming resolution.

## 3. Ban on Absolute Claims
- Words such as "kusursuz", "%100 dayanıklı", "tamamen hatasız", "her senaryoya karşı tam korumalı" are strictly prohibited in technical reports.
- Every claim must state its operational boundaries and assumptions:
  - Tested conditions
  - Untested conditions
  - Environmental prerequisites

## 4. Storage & Infrastructure Specificity
- State clearly whether tests execute against `InMemoryWebhookStore`, mock stores, or production engines (e.g. PostgreSQL, Redis, SQL Server).
- In-memory lock/lease semantics do not prove network partition resilience or distributed locking semantics; distinguish engine boundaries explicitly.

## 5. Structured Claim-Evidence Matrix
Every completion report or PR description for fixes must include a structured Claim-Evidence table:

| İddia (Claim) | Kanıt (dosya:satır) | Test Adı | Önce/Sonra Durumu | Çalıştırma Sayısı / Sonuç | Kapsam Dışı / Sınırlar |
| :--- | :--- | :--- | :--- | :--- | :--- |

## 6. Independent Verification Pass (Cross-Checking)
- When generating reports, cross-check cited file paths, method names, and line numbers directly against the source tree before finalizing.
