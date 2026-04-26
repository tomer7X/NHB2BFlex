# NHB2BFlex Command - Usage Instructions

## Overview
The **NHB2BFlex** command simplifies block generation by using a Production Instruction (PI) template block to generate output blocks based on selected Front blocks.

## Workflow Steps

### STEP 1: Select Front Blocks
**What to do:**
- In AutoCAD, use the command: `NHB2BFlex`
- When prompted, **select all FRONT blocks** you want to process
- All selected blocks **MUST be of the same block type**

**Important Notes:**
- You can select multiple instances of the same block type
- If you select blocks of different types, the command will reject them and ask you to try again
- Only block references will be processed (other objects are ignored)

**Example:** You can select 5 instances of "BLOCK-A", but NOT a mix of "BLOCK-A" and "BLOCK-B"

---

### STEP 2: Select the PI (Production Instruction) Block
**What to do:**
- When prompted, **click on a single PI (Production Instruction) block**
- This block serves as the template for generating output blocks
- The PI block's attributes/properties will be used as fallback values

**Important Notes:**
- The PI block must have attributes and properties that are **contained in** the Front blocks
- If the PI block has attributes/properties that don't exist in the Front blocks, the command will reject it
- You can only select one PI block

**Validation:** The command checks that all PI block attributes/properties exist in the Front blocks. If validation fails, you'll see a detailed error message listing what's missing.

---

### STEP 3: Select the NET Block
**What to do:**
- When prompted, **click on the NET block** (the grid/container block)
- This is where all generated blocks will be placed

**Important Notes:**
- The NET block defines the grid layout (10 columns by default)
- Output blocks will be arranged in a grid starting from the top-left corner
- The command calculates optimal cell sizes based on the NET block dimensions

---

## What Happens Next

Once you've made all selections, the command will:

1. **Validate** that all Front blocks are the same type
2. **Validate** that the PI block is compatible with Front blocks
3. **Group** Front blocks by their attribute/property values
   - Identical blocks are combined into one output block
   - The combined block's **Quantity** attribute reflects how many blocks were grouped
4. **Generate** output blocks using the PI template
5. **Place** generated blocks in a grid layout within the NET block
6. **Detect conflicts** - if multiple unique blocks share the same NAME value, red revclouds are drawn around them for easy identification

---

## Message Types During Execution

- ✓ **Success messages** - Operation completed successfully
- ✗ **Error messages** - Something went wrong; read the details and correct the issue
- ⚠ **Warnings** - Alert about potential issues (like NAME conflicts)

---

## Example Scenario

**Scenario:** Processing 3 Front blocks (A, A, B) with PI template "TEMPLATE-1"

1. Select Front blocks: BLOCK-1 instance 1, BLOCK-1 instance 2, BLOCK-1 instance 3
2. Select PI block: TEMPLATE-1
3. Select NET block: NET-GRID

**Output:**
- Checks that all 3 are the same type ✓
- Groups them: 2 blocks with Config-A, 1 block with Config-B
- Generates 2 output blocks:
  - Block 1: Quantity=2 (combined A's)
  - Block 2: Quantity=1 (B)
- Places them in the NET grid at positions [0,0] and [0,1]

---

## Troubleshooting

### "Error: Selected blocks are of different types"
- Make sure all Front blocks are the same block type
- Only instances of the same block are allowed

### "Error: PI block has attributes/properties not found in Front block"
- The PI block is incompatible with the Front blocks
- Select a PI block that has fewer or equal attributes/properties
- The error message will tell you which attributes are missing

### "No block references found in selection"
- You selected objects that are not blocks
- Make sure to select block references, not other geometry

---

## Tips

- **Before running the command:** Make sure the PI block you plan to use has the right template attributes/properties
- **NAME conflicts:** If the command draws red revclouds, it means multiple different block configurations share the same NAME value - review and fix as needed
- **Quantity tracking:** Always check the Quantity attribute of generated blocks to ensure correct grouping

---

## Command Summary

| Step | Select | Purpose |
|------|--------|---------|
| 1 | Front Blocks (multiple, same type) | Source data to process |
| 2 | PI Block (single) | Template for output blocks |
| 3 | NET Block (single) | Grid container for placement |

