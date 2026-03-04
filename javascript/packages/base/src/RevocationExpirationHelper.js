/**
 * Helper for checking attestation revocation and expiration status.
 * Centralizes the lifecycle policy for determining if an attestation is revoked or expired.
 *
 * Policy:
 * - Revoked: attestation.revoked === true (attester explicitly revoked it)
 * - Expired: expirationTime is set to a past Unix timestamp (in seconds)
 *
 * EAS sentinel: 0 (Unix epoch = 1970-01-01) means "no expiration". Negative is also treated
 * as sentinel. Never treat 0 as "expired at epoch" — it means "never expires".
 *
 * This policy is critical for security and must be enforced consistently
 * in all attestation validation paths (stage 1, specialists, chain walks, subject validation).
 */

/**
 * Checks if an attestation has been revoked by its attester.
 *
 * An attestation is revoked if the `revoked` property is explicitly set to true.
 *
 * Returns false if attestation is null/undefined (defensive).
 *
 * @param {Object} attestation - The attestation to check
 * @returns {boolean} True if the attestation is revoked
 */
export function isRevoked(attestation) {
  if (!attestation) {
    return false;
  }

  return attestation.revoked === true;
}

/**
 * Checks if an attestation has expired (passed its validity window).
 *
 * An attestation is expired if:
 * - expirationTime is set to a positive value (Unix timestamp in seconds)
 * - AND expirationTime is earlier than the current time
 * - If expirationTime is 0, negative, or unset, the attestation does not expire (EAS sentinel:
 *   0 = Unix epoch 1970-01-01 = "no expiration"; must not be treated as "expired at epoch").
 *
 * Handles both EAS SDK formats:
 * - EAS JavaScript SDK: `expirationTime` (number or BigNumber in seconds)
 * - Generic: `expirationDateTime` (also in seconds)
 * - String format: parses numeric string to integer
 *
 * Returns false if attestation is null/undefined (defensive).
 *
 * @param {Object} attestation - The attestation to check
 * @returns {boolean} True if the attestation is expired
 */
export function isExpired(attestation) {
  if (!attestation) {
    return false;
  }

  const raw = attestation.expirationTime ?? attestation.expirationDateTime;
  if (raw === undefined || raw === null) {
    return false;
  }

  // Normalize to number (EAS uses seconds since epoch; SDK may return BigNumber)
  const expirationSeconds = typeof raw === 'string'
    ? parseInt(raw, 10)
    : (typeof raw === 'bigint' ? Number(raw) : (typeof raw?.toNumber === 'function' ? raw.toNumber() : Number(raw)));

  if (!Number.isFinite(expirationSeconds) || expirationSeconds <= 0) {
    // 0 or negative = EAS sentinel "no expiration" (0 = epoch 1970)
    return false;
  }

  const nowSeconds = Math.floor(Date.now() / 1000);
  return expirationSeconds < nowSeconds;
}

/**
 * Checks both revocation and expiration status in a single call.
 *
 * @param {Object} attestation - The attestation to check
 * @returns {{isRevoked: boolean, isExpired: boolean}} Object with both checks
 */
export function checkRevocationAndExpiration(attestation) {
  return {
    isRevoked: isRevoked(attestation),
    isExpired: isExpired(attestation)
  };
}
