# Evoq.Blockchain Hex Type Reference

**Source:** `/Users/lukepuplett/Git/Hub/evoq-blockchain/src/Evoq.Blockchain/Blockchain/Hex.cs`

A readonly struct that wraps a byte array to represent hex values in blockchain operations. It's a core value type in the Evoq libraries.

## Core Semantics

### Constructor
```csharp
public Hex(byte[] value)
```
- Takes a byte array (throws if null, tolerates empty arrays)
- Stores reference to the array
- Never null itself (it's a struct)

### Static Constants
- `Hex.Empty` - Zero-length array (represents "0x")
- `Hex.Zero` - Single zero byte [0] (represents "0x0")

### Length Property
```csharp
public int Length => value?.Length ?? 0;
```
Returns the byte array length. Empty Hex has Length 0.

## Parsing & Conversion

### Parse (String → Hex)
```csharp
public static Hex Parse(string hex, HexParseOptions options = Strict)
```

**Behavior:**
- Strips "0x" prefix if present
- **Throws `ArgumentNullException`** with message **"Hex string cannot be null"** if hex is null
- Throws `ArgumentException` if empty string (unless `AllowEmptyString` flag set)
- Validates all characters are valid hex digits
- Converts pairs of hex digits to bytes

**Example:**
```csharp
var h1 = Hex.Parse("0xabcd");           // [0xAB, 0xCD]
var h2 = Hex.Parse("0x");               // Throws (empty after prefix removal)
var h3 = Hex.Parse("");                 // Throws (empty string)
var h4 = Hex.Parse("", HexParseOptions.AllowEmptyString);  // Returns Hex.Empty
```

### ToString (Hex → String)
```csharp
public string ToString(bool trimLeadingZeroDigits = false)
```

**Behavior:**
- Returns "0x" for empty/default(Hex)
- Returns "0x0" for single zero byte
- Returns "0xABCD..." for normal values
- Can trim leading zero digits if requested

### Implicit Conversion
```csharp
public static implicit operator Hex(string hex) => Parse(hex);
```

Allows: `Hex myHex = "0xabcd";`

### BigInteger Conversion
- `ToBigInteger(HexSignedness, HexEndianness)` - Convert to BigInteger (default: unsigned, big-endian)
- `FromBigInteger(BigInteger, HexEndianness)` - Create Hex from BigInteger

### Byte Array Conversion
- `ToByteArray()` - Returns copy of internal byte array
- `FromBytes(byte[], reverseEndianness, trimLeadingZeros)` - Create from byte array with options

## Critical Validation Methods

### IsEmpty()
```csharp
public bool IsEmpty()
{
    if (value == null) return true;
    return value.Length == 0;
}
```

**Returns true ONLY if byte array length is 0.**

Does NOT return true for zero values!

```csharp
var empty = new Hex(Array.Empty<byte>());           // IsEmpty() = true
var zeros = new Hex(new byte[32]);                  // IsEmpty() = false (length 32!)
```

### IsZeroValue()
```csharp
public bool IsZeroValue()
{
    if (value == null) return false;
    return value.All(b => b == 0);
}
```

**Returns true if ALL bytes are zero (0x00...00).**

Regardless of length:

```csharp
var zeros32 = new Hex(new byte[32]);               // IsZeroValue() = true
var zeros1 = new Hex(new byte[1]);                 // IsZeroValue() = true
var nonzero = new Hex(new byte[] { 0, 1 });       // IsZeroValue() = false
```

### ValueEquals(Hex other)
Compares numerical value ignoring leading zeros. Both 0x0, 0x00, 0x0000 are equal.

## HexParseOptions Flags

```csharp
[Flags]
public enum HexParseOptions
{
    Strict = 0,              // Default: strict parsing
    AllowOddLength = 1,      // Pad odd-length strings with leading 0
    AllowEmptyString = 2,    // Treat "" as Hex.Empty
    AllowNullString = 4      // Treat null as Hex.Empty
}
```

**Example:**
```csharp
// Throws ArgumentException (empty string)
Hex.Parse("");

// Returns Hex.Empty
Hex.Parse("", HexParseOptions.AllowEmptyString);

// Pads to "0x01" then parses
Hex.Parse("0x1", HexParseOptions.AllowOddLength);
```

## Equality

- `Equals(Hex other)` and `operator ==`/`!=` compare byte arrays exactly
- Handles null edge cases for `default(Hex)`
- `GetHashCode()` uses string representation

## Common Patterns

### Creating a Zero/Dummy Hex
```csharp
// For dummy values (e.g., for read-only operations)
var dummy = new Hex(new byte[32]);  // 32 zero bytes, Length = 32
```

### Detecting Invalid/Meaningless UIDs
```csharp
// WRONG - catches truly empty only
if (uid.IsEmpty()) { /* reject */ }

// CORRECT - catches zero-value hashes
if (uid.IsZeroValue()) { /* reject */ }

// BETTER - explicit check for meaningful UIDs
if (uid.Length == 0 || uid.IsZeroValue()) { /* reject */ }
```

### Safe Parsing with Options
```csharp
if (Hex.TryParse(userInput, out var hex))
{
    // Safe, no exception
}
```

## Key Gotchas

1. **IsEmpty() vs IsZeroValue():** Don't confuse them. IsEmpty checks length, IsZeroValue checks all bytes.

2. **Hex string cannot be null:** The error message "Hex string cannot be null" comes from Parse when given null without AllowNullString flag.

3. **Default(Hex) handling:** Default-initialized Hex has `value = null`, not empty array. IsEmpty() returns true for default(Hex).

4. **Length semantics:**
   - `new Hex(Array.Empty<byte>())` → Length = 0
   - `new Hex(new byte[32])` → Length = 32 (not 0!)

5. **String conversion:** ToString() always includes "0x" prefix. No direct way to get raw hex without prefix.

## Related Types

- `HexSignedness` - Signed vs Unsigned for BigInteger conversion
- `HexEndianness` - BigEndian (conventional) vs LittleEndian (for BigInteger)
- `IByteArray` interface - Hex implements this
