using System.Security.Cryptography;

namespace Exsensic.Core.Bookings;

/// <summary>
/// Creates the human-readable booking reference shown to clients, for example "EXS-7K2M9QXA".
/// </summary>
/// <remarks>
/// The reference is random rather than a running number, so it doesn't reveal how many bookings exist
/// and can't be guessed from another one. Letters and digits that are easy to confuse (0/O, 1/I/L) are
/// left out so a client can read it out over the phone. With 31^8 (about 850 billion) combinations a clash is extremely
/// unlikely, and the unique index on Bookings.Reference rejects one if it ever happens.
/// </remarks>
public static class BookingReferenceGenerator
{
    /// <summary>The fixed start of every reference.</summary>
    public const string Prefix = "EXS-";

    /// <summary>How many random characters follow the prefix.</summary>
    public const int RandomLength = 8;

    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    /// <summary>
    /// Returns a new random reference, using a cryptographic random generator so references can't be predicted.
    /// </summary>
    public static string Next() => Prefix + new string(RandomNumberGenerator.GetItems<char>(Alphabet, RandomLength));
}
