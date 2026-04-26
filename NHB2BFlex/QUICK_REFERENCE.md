# NHB2BFlex Quick Reference

## Command: `NHB2BFlex`

### Three Simple Steps:

```
┌─────────────────────────────────────────┐
│     STEP 1: Select Front Blocks        │
│  (multiple of the SAME type)           │
│                                         │
│  ✓ Can select multiple instances       │
│  ✗ Cannot mix different block types    │
└─────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────┐
│    STEP 2: Select PI Block             │
│  (Production Instruction template)      │
│                                         │
│  ✓ Must be compatible with Front block │
│  ✗ Will reject incompatible PI blocks  │
└─────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────┐
│     STEP 3: Select NET Block           │
│   (Grid container for output)          │
│                                         │
│  ✓ Defines placement grid (10 cols)    │
│  ✗ Cannot skip this step               │
└─────────────────────────────────────────┘
                    ↓
           ✓ Done! Blocks generated
```

## What the Command Does

| Input | Output |
|-------|--------|
| Multiple Front blocks (same type) | Grouped output blocks in NET grid |
| 1 PI template block | Used as basis for generated blocks |
| 1 NET block | Grid structure for placement |

**Key Feature:** Automatically combines Front blocks with identical attributes into a single output block with a Quantity count.

## Messages in Console

- `========== NHB2BFlex Command Started ==========` → Command starting
- `--- STEP 1/2/3 ---` → Instructions for each step
- `✓ message` → Success/validation passed
- `✗ Error: message` → Something wrong - read the error details
- `⚠ WARNING` → Alerts about issues (like NAME conflicts)
- `========== NHB2BFlex Command Completed Successfully ==========` → All done!

## Common Issues

| Issue | Solution |
|-------|----------|
| "Different types" error | Select only one block type |
| "No block references" error | Select blocks, not other objects |
| "PI block incompatible" error | Use a different PI block |
| Red revclouds appear | Multiple blocks share same NAME - review them |

## Grid Layout

Generated blocks are placed in a 10-column grid within the NET block:

```
[0,0] [0,1] [0,2] ... [0,9]
[1,0] [1,1] [1,2] ... [1,9]
[2,0] [2,1] [2,2] ... [2,9]
  ...
```

Each cell is auto-sized based on the NET block dimensions.

---

**Need more details?** See `USAGE_INSTRUCTIONS.md`
