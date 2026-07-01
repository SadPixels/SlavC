# Reproducible builds

Roslyn deterministic emit, normalized virtual source paths, ordinal entry ordering,
stable JSON property ordering, and absent timestamps make unsigned output
reproducible for identical inputs and host templates. Absolute user paths are not
stored in release manifests. Platform code signing changes bytes and is outside
the unsigned reproducibility guarantee.
