# Error Handling

This document describes how the API communicates errors to callers. There are three distinct patterns, each with a different purpose.

---

## Pattern A — External Proxy Validation (200 OK + `errorMessage`)

**Used by:** Bodacc, INSEE, ChIde (both endpoints), IBAN, VIES `check-active`

These endpoints proxy to an external service. The primary job is to relay whether the external service accepted or rejected the identifier. Because the call itself succeeded (no internal fault), the HTTP status is always **200 OK** — the validation verdict lives in the response body.

| Field | Meaning |
|---|---|
| `errorMessage: null` | External service accepted the request — treat as valid |
| `errorMessage: "..."` | External service rejected the request or returned an error — treat as invalid |

```json
// Accepted
{ "isValid": true,  "errorMessage": null }

// Rejected by external service
{ "isValid": false, "errorMessage": "VAT number not found in VIES." }
```

All Pattern A response models implement `IExternalValidationResponse` (see `src/Shared/Contracts/IExternalValidationResponse.cs`), making the contract explicit in the type system.

---

## Pattern B — Local Computation (200 OK, `isValid` only)

**Used by:** VIES `validate-format`

These endpoints run pure local logic with no external call. They return only `isValid`; there is no external error source to relay. HTTP status is always **200 OK**.

```json
{ "isValid": false }
```

---

## Pattern B+ — Local Computation with Error Detail (200 OK + `errorMessage`)

**Used by:** TVA Calculator

Same as Pattern B but includes `errorMessage` to explain *why* the input was rejected (e.g., `"SIREN must contain at least 9 digits."`). HTTP status is always **200 OK**.

```json
// Valid
{ "siren": "443061841", "tvaNumber": "FR64443061841", "isValid": true, "errorMessage": null }

// Invalid input (passed request validation but failed service logic)
{ "siren": "123", "isValid": false, "errorMessage": "SIREN must contain at least 9 digits." }
```

---

## Pattern C — Input Validation & Internal Exceptions (4xx / 5xx)

**Used by:** all endpoints (request validation layer + unhandled exceptions)

Triggered **before** any service logic runs. Returns a non-200 HTTP status and a different response schema (`ErrorResponse`).

| Trigger | HTTP Status | Schema |
|---|---|---|
| Missing / structurally invalid field (`FluentValidation`) | `400 Bad Request` | `{ "statusCode": 400, "message": "..." }` |
| `ArgumentException`, `FormatException`, `ValidationException` | `400 Bad Request` | `{ "statusCode": 400, "message": "..." }` |
| `HttpRequestException` with upstream status | Upstream status (or `502`) | `{ "statusCode": N, "message": "..." }` |
| Unhandled exception | `500 Internal Server Error` | `{ "statusCode": 500, "message": "Internal server error" }` |

```json
// 400 — SIREN field missing entirely
HTTP 400
{ "statusCode": 400, "message": "SIREN number is required." }
```

---

## Decision rationale

The API is fundamentally a **validation relay**: its value is in telling callers whether a French SIREN, Swiss UID, IBAN, or VAT number is valid according to the authoritative external registry. A `200 OK` response means "the service ran successfully and here is the verdict." A `4xx` means "the request itself was malformed and no lookup was attempted."

This avoids ambiguity between "your IBAN is invalid" (a legitimate business result) and "your request was malformed" (a client programming error).

---

## Per-feature reference

| Feature | Endpoint | Pattern | `errorMessage` source |
|---|---|---|---|
| Bodacc | `GET /bodacc/search` | A | BODACC API |
| INSEE | `GET /insee/establishments/{siret}` | A | INSEE SIRENE API |
| ChIde | `GET /ch-ide/{uid}` | A | CH IDE SOAP service |
| ChIde | `GET /ch-ide/{uid}/validate` | A | CH IDE SOAP service |
| IBAN | `GET /iban/verify` | A | Local rules + external BIC lookup |
| VIES | `GET /vies/check-active` | A | EU VIES SOAP service |
| VIES | `GET /vies/validate-format` | B | *(none — local only)* |
| TVA Calculator | `GET /tva-calculator` | B+ | Local validation logic |
