# RusPack format v1

Layout: `[host][entries][UTF-8 JSON manifest][128-byte footer]`.

All integers and absolute offsets are little-endian. The footer begins with
`RUSPACK\0`; no scanning is permitted. CRC-32 authenticates the footer before the
manifest is allocated. SHA-256 authenticates the stored entry region. Each entry
also carries the SHA-256 of its decompressed content.

Limits in v1: 16 MiB manifest, 4096 entries, 1 GiB stored entry, and 2 GiB
decompressed entry. Names are relative logical paths using `/`; absolute paths,
empty segments, `.` and `..` are invalid.

Format v1 supports uncompressed and Brotli-compressed entries. Native library
entries and signed manifests are reserved and are not supported by the MVP.
