/**
 * Helper for checking attestation revocation and expiration status.
 * Centralizes the policy for determining if an attestation is revoked or expired.
 *
 * Policy is defined in docs/REVOCATION_EXPIRATION_POLICY.md.
 */

/**
 * Checks if an attestation has been revoked.
 *
 * An attestation is considered revoked if:
 * - The `revoked` property is set to true
 *
 * See REVOCATION_EXPIRATION_POLICY.md for policy details.
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
 * Checks if an attestation has expired.
 *
 * An attestation is considered expired if:
 * - The `expirationTime` property is set to a non-zero value (in Unix seconds)
 * - And the current time is past that expiration time
 * - If `expirationTime` is 0 or unset, the attestation does not expire
 *
 * Handles both EAS SDK formats:
 * - EAS JavaScript SDK: `expirationTime` (number in seconds)
 * - Generic: `expirationDateTime` (also in seconds)
 *
 * See REVOCATION_EXPIRATION_POLICY.md for policy details.
 *
 * @param {Object} attestation - The attestation to check
 * @returns {boolean} True if the attestation is expired
 */
export function isExpired(attestation) {
  if (!attestation) {
    return false;
  }

  // Handle both EAS attestation types and generic attestations
  const expirationTime = attestation.expirationTime || attestation.expirationDateTime;

  if (!expirationTime) {
    // No expiration time = never expires
    return false;
  }

  // Convert to number of seconds (EAS uses seconds since epoch)
  const expirationSeconds = typeof expirationTime === 'string'
    ? parseInt(expirationTime, 10)
    : expirationTime;

  if (!Number.isInteger(expirationSeconds) || expirationSeconds === 0) {
    // 0 means no expiration
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
