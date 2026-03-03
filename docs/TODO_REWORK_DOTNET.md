

  1. Map spec ↔ current .NET

  Spec
  • Single pipeline: Validate(attestation, context) → record visit + depth → Stage 1
    (expired, revoked, schema) → Stage 2 (specialist(attestation, context)).
  • Context: optional Merkle root, optional extension, seen set (record visit, fail
    if cycle), depth (increment on recurse, fail if over limit).
  • Specialists: in = (attestation, context), out = result; may call
    Validate(referenced, context).
  • Stage 1: no attester; attester is per-specialist.
  • Failure: fetch failure and validation failures bubble; each level returns a
    failure with the callee’s failure as inner failure.

  Current .NET
  • No single `Validate(attestation, context)`. Reader calls
    attestationVerifierFactory.GetVerifier(serviceId) then
    verifier.VerifyAsync(attestation, merkleRoot). No shared context, no seen
    set/depth.
  • No context type. Merkle root is passed as Hex merkleRoot; no “context” object.
  • Verifiers = “do everything”: each IAttestationVerifier.VerifyAsync(attestation, 
    merkleRoot) does its own expiry, revocation, schema, attester, and (where
    relevant) Merkle. IsDelegate does a loop (walk chain) and inlines subject fetch +
    “outer” checks + payload validator; it does not call a shared Validate again.
  • Routing: by service ID (e.g. eas-is-delegate, eas) from attestation +
    AttestationRoutingConfig, not by “schema → specialist” in the spec sense (though
    it’s schema-driven).
  • Failure chain: AttestationResult already has InnerAttestationResult; we should
    ensure it’s set when bubbling.

  So the gap is: no shared pipeline, no context (with seen/depth), no recursion into a 
  single Validate, and Stage 1 / attester split doesn’t match the spec.

  ────────────────────────────────────────



  2. Approach for .NET (high level)

  A. Introduce a validation context type (spec §4)
  • Add a context type (e.g. AttestationValidationContext or ValidationContext) that
    holds:
    • Optional Merkle root (e.g. Hex? MerkleRoot).
    • Optional extension (e.g. IDictionary<string, object> Extension or a small,
      typed bag).
    • Seen set: e.g. HashSet<string> of attestation UIDs, plus a method like void 
      RecordVisit(string attestationUid) that throws (or returns a result) if already
      seen.
    • Depth: current depth and max depth, plus e.g. void EnterRecursion() / void 
      ExitRecursion() that throw if over max (and optionally a IDisposable or scope
      so exit is guaranteed).
  • Put this in core (e.g. under Zipwire.ProofPack) so both core and Ethereum can use
    it; or in the Ethereum package if you want to keep core minimal.
  • Default max depth (e.g. 32) and initial empty seen set.

  B. Define the single pipeline entry point
  • Add something like ValidateAsync(attestation, context) (or a small type that
    holds this plus the specialist registry).
  • Before Stage 1: call context.RecordVisit(attestation.Uid) (or equivalent); if it
    throws (cycle), return failure and set inner if applicable.
  • Before recursing: call context.EnterRecursion() (or increment depth); if over
    limit, return failure; on exit from recursion, ExitRecursion().
  • Stage 1: run shared checks: not expired, not revoked, schema recognized. “Schema
    recognized” = we have a specialist for this schema (or we have an explicit
    allowed-schema set). If any fail, return failure (with inner if we already had
    one).
  • Stage 2: resolve specialist for this attestation’s schema (see below), then call
    the specialist with (attestation, context). The specialist must be able to call
    back into ValidateAsync(referencedAttestation, context) when it follows a RefUID.

  • So the pipeline needs a way to call itself (or a delegate) so that specialists can
     recurse. That implies either: the pipeline is a class that holds a reference to
    itself / to a ValidateAsync delegate, or we pass a ValidateAsync delegate into the
     context or into each specialist.

  C. Schema → specialist resolution (routing)
  • Spec says “load specialist for schema”. Today we have service ID (e.g.
    eas-is-delegate, eas) from GetServiceIdFromAttestation(attestation, 
    routingConfig).
  • Option 1: Keep service ID as the key; the “specialist registry” is the existing
    AttestationVerifierFactory keyed by service ID. We just change the signature of
    what we call: instead of VerifyAsync(attestation, merkleRoot), we have something
    like VerifyAsync(attestation, context) and the pipeline passes context (with
    MerkleRoot set from context when needed).
  • Option 2: Introduce a separate “schema UID → specialist” registry and use that
    for Stage 2; service ID could still be used for “which verifier handles this
    attestation” if we keep routing as-is for the first dispatch.
  • Easiest short-term: keep routing → service ID → GetVerifier(serviceId). The
    “specialist” is that verifier. We add an overload or new interface that takes
    (attestation, context) and a callback Func<Attestation, ValidationContext, 
    Task<AttestationResult>> ValidateAsync so the verifier can recurse.

  D. Verifiers become specialists (signature + recursion)
  • Today: Task<AttestationResult> VerifyAsync(MerklePayloadAttestation attestation, 
    Hex merkleRoot).
  • Spec: specialist gets (attestation, context) and can call
    ValidateAsync(referencedAttestation, context).
  • So we need either:
    • New interface (e.g. IAttestationSpecialist) with VerifyAsync(attestation, 
      context, ValidateAsync recurse) and the pipeline calls that; or
    • Existing interface extended with an overload that takes context and a
      ValidateAsync delegate; or
    • Context carries the delegate: e.g. context.ValidateAsync(attestation) so the
      specialist just calls context.ValidateAsync(fetchedAttestation) and doesn’t
      need an extra parameter.
  • Recommendation: add a context type that carries `ValidateAsync` (or a
    ValidateAsync delegate on the context). So when a specialist wants to recurse, it
    calls await context.ValidateAsync(fetchedAttestation). That keeps the specialist
    signature as (attestation, context) and matches the spec. The pipeline, when it
    constructs the context, sets context.ValidateAsync = (att) => ValidateAsync(att, 
    context) (or the same for an instance method).

  E. Stage 1: extract shared checks, attester in specialist
  • Extract “not expired”, “not revoked”, “schema recognized” into a shared helper
    (e.g. ValidateStage1Async(attestation, context) or a small type that the pipeline
    calls).
  • Schema recognized: either (i) “we have a specialist for this schema” (so the
    pipeline looks up by schema; if none, fail Stage 1), or (ii) an explicit “allowed
    schema” set in config. (i) fits “registry of specialists per schema” and avoids
    duplicate config.)
  • Attester: remove from shared Stage 1. Each specialist that cares (Human,
    PrivateData) does “attester in allowlist for my schema” inside its own logic.
    IsDelegate specialist does not do allowlist on delegation links (or only on
    root); it does authority continuity and whatever root policy we want. So we
    refactor existing verifiers to move attester checks out of any shared path and
    into the specialist logic.

  F. IsDelegate refactor: loop → recursion
  • Today: WalkChainToTrustedRootAsync is a loop: fetch attestation, check
    revoked/expired/continuity, if delegation link then currentUid = refUid, else if
    accepted root then fetch subject and run subject validation inline.
  • Spec: specialist can only “fetch refUID and call Validate(referenced, context)”.
    So one recursion per RefUID. That means the IsDelegate “specialist” for a
    delegation link attestation would: check authority continuity (and any link-level
    checks), then call context.ValidateAsync(attestationAtRefUid). The next
    attestation might be another delegation link (same or different schema) or a root
    schema; the pipeline runs Stage 1 + Stage 2 for it, so we get another specialist
    (maybe IsDelegate again for the next link, or Human/PrivateData at root). So the
    “chain walk” becomes repeated recursion: at each step the delegation specialist
    fetches parent and calls Validate(parent, context).
  • That implies we need a single specialist for “delegation schema” that: (1) does
    link-level checks (e.g. leaf recipient, authority continuity from previous
    attestation — but “previous” is tricky if we’re not in a loop; see below), (2)
    calls context.ValidateAsync(attestationAtRefUid). The “previous” attestation for
    continuity could be passed in context (e.g. “last attester” or “parent
    attestation”) so the next link can check recipient == previous attester. So
    context might grow an optional “previous attester” or “parent attestation” that
    the delegation specialist sets when it’s about to recurse, and the child (next
    link or root) reads it for continuity.
  • Alternative: keep the loop inside the IsDelegate specialist for the chain walk 
    only, and use recursion only for the subject at the root. So: when we hit an
    accepted root, we don’t recurse for the root itself; we fetch subject and call
    Validate(subject, context). That’s a smaller change and still matches “failure
    bubbles with inner” and “context has seen/depth”. The spec allows “specialist may
    recurse”; it doesn’t force every hop to be one recursion. So we could do:
    IsDelegate specialist = “walk chain in a loop (with context.RecordVisit and depth
    at each hop), and when at root, fetch subject and call
    context.ValidateAsync(subject)”. That reuses the current loop structure but plugs
    in context and a single recursive call for the subject.

  G. Failure chain
  • We already have AttestationResult.Failure(..., innerResult). When any
    verifier/specialist returns failure after a recursive ValidateAsync failed, we
    construct the failure with innerResult = the result from the recursive call. So
    we audit all places that call ValidateAsync (or the current equivalent) and
    ensure we pass through the inner result when bubbling. No new type needed; just
    discipline.

  H. Where the pipeline lives
  • Option A: New type in core, e.g. AttestationValidationPipeline, with
    ValidateAsync(attestation, context). It takes a “resolver” (schema or service ID
    → specialist) and runs Stage 1 + Stage 2. The reader (or the verification
    context) creates the context and calls
    pipeline.ValidateAsync(attestedDocument.Attestation, context) instead of calling
    the verifier directly.
  • Option B: The pipeline lives inside the reader or inside a wrapper that the reader
     uses. So when the reader has an attested document, it builds context (with
    merkleRoot from the document, seen set, depth, and a delegate that points back to
    the same pipeline), then calls the pipeline once with the document’s attestation.

  • I’d put the pipeline in core (or in the first place that has “attestation +
    Merkle” verification) and have the reader call it so that “squaring off” is:
    reader builds context and calls pipeline; pipeline is the only place that does
    record visit, depth, Stage 1, Stage 2.


  ────────────────────────────────────────



  3. Suggested order of work (.NET)

  1. Context type — Add AttestationValidationContext (or similar) with optional
     Merkle root, extension, seen set + RecordVisit(uid), depth + enter/exit
     recursion, and a ValidateAsync(attestation) delegate (or equivalent).
  2. Pipeline entry point — Add ValidateAsync(attestation, context) that: records
     visit (and fails if cycle), checks depth (and fails if over), runs Stage 1
     (expired, revoked, schema recognized), runs Stage 2 (resolve specialist, call
     specialist with attestation + context). Wire context.ValidateAsync to this
     method.
  3. Stage 1 helper — Extract expired, revoked, schema-recognized into a shared
     helper; call it from the pipeline. Decide “schema recognized” = has specialist
     for schema (or explicit set).
  4. Specialist interface / overload — Add a way to call a verifier with
     (attestation, context) and a way for it to call
     context.ValidateAsync(referencedAttestation). E.g. new interface or overload;
     implement for EAS and IsDelegate.
  5. Reader integration — When the reader verifies an attestation, build context
     (merkleRoot from document, empty seen set, depth 0, max depth from config), set
     context.ValidateAsync to the pipeline’s ValidateAsync, then call
     ValidateAsync(document.Attestation, context) instead of calling the verifier
     directly with only merkleRoot.
  6. Attester in specialists — Move attester checks out of any shared Stage 1 path
     into EAS/PrivateData and IsDelegate specialists (IsDelegate only where desired,
     e.g. root).
  7. Failure chain — Ensure every place that returns after a recursive failure sets
     InnerAttestationResult to the recursive result.
  8. IsDelegate refactor — Either (a) refactor to “loop + single recursion for
     subject” with context and RecordVisit/depth in the loop, or (b) full recursion
     per hop with “previous attester” (or similar) in context for continuity. Prefer
     (a) first for fewer changes.

  That’s the approach: introduce context and a single pipeline, then gradually make
  verifiers “specialists” that take context and recurse via context.ValidateAsync, and
  align Stage 1 / attester and failure handling with the spec. JavaScript can follow
  the same structure once .NET is clear.

