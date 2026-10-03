using SonglistSpinner.Core.StreamerSongList;

namespace SonglistSpinner.Services;

/// <summary>
/// The API credential fields as the Settings page holds them. The token itself is never kept here: only whether
/// one has been entered, because a blank token field means "keep the saved token".
/// </summary>
public sealed record CredentialDraft(StreamerSongListCredentialKind Kind, string ClientId, bool TokenEntered);
