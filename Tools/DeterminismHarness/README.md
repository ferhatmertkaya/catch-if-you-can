# Determinism Harness

Runs the deterministic generation suite **outside Unity**.

This is possible because `Assets/CatchIfYouCan/Scripts/Procedural/Deterministic` references
no `UnityEngine` type — the same property that makes generation deterministic in the first
place. Its assembly definition sets `"noEngineReferences": true`, so the compiler enforces
it rather than a code review.

```bash
# full suite (what CI runs)
dotnet run --project Tools/DeterminismHarness -- test

# regenerate the committed golden seed table
dotnet run --project Tools/DeterminismHarness -- golden \
  > Assets/CatchIfYouCan/Scripts/Procedural/Deterministic/GoldenSeedTable.cs

# hash report for one seed
dotnet run --project Tools/DeterminismHarness -- report 184726392

# static guard + suite together
Scripts/check_determinism.sh
```

The Unity EditMode tests in `Assets/CatchIfYouCan/Tests/EditMode` assert the same things.
Both exist deliberately: this harness runs in CI with no Unity licence, while the EditMode
tests additionally prove that `UnityEngine.Random` cannot perturb generation inside the real
engine, and that Unity's own toolchain (Mono in the editor, IL2CPP on device) produces the
same hashes.

**Regenerating goldens is not a way to fix a failing test.** A golden failure means layouts
changed for every stored seed. Either revert the change, or bump `GenerationVersion` and
regenerate in the same commit.
