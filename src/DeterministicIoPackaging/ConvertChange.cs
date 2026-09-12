namespace DeterministicIoPackaging;

/// <summary>
/// One thing <see cref="DeterministicPackage"/> changed while converting a package.
/// </summary>
/// <param name="Kind">What was done.</param>
/// <param name="Entry">
/// The archive entry it was done to, or <c>null</c> for a change that applies to the package as a
/// whole (<see cref="ConvertChangeKind.Reordered"/>).
/// </param>
/// <remarks>
/// Only differences are reported. The conversion also restamps every timestamp, recompresses every
/// entry and reserializes every XML part, but those happen to every input alike and produce the same
/// bytes on a second conversion, so reporting them would say nothing about why a given package was
/// not already deterministic.
/// </remarks>
public record ConvertChange(ConvertChangeKind Kind, string? Entry = null);
