# Warehouse Pack — Getting Started

Low-poly warehouse props for Unity. This guide covers everything you need to start using them.

## What's included

One complete pick → pack → ship loop:

- **Pallet Rack Start Bay** — two uprights + beam levels
- **Pallet Rack Add-On Bay** — one upright + beams, shares its neighbour's; tile along the aisle
- **Rack Upright Post Protector**
- **Boltless Shelving Unit**
- **Stacking Tote**
- **Sectional Overhead Door** — panels as separate objects for animation
- **Dock Bumper**
- **Euro Pallet**
- **Packing Table**
- **Assembled Cardboard Box**
- **Shipped Parcel** — carton with a label
- **Hand Pallet Jack**
- **Floor-Line Kit** — straight / corner / T / cross / end-cap / hatch pieces
- **Blank A-Frame Sign**
- **High-Bay LED Light** — pendant luminaire with an emissive lens

## Render pipeline

Warehouse Pack is **URP only**. All prefabs live in `Prefabs/URP/` and their materials use
Universal Render Pipeline shaders. Make sure your project is set to URP
(Project Settings → Graphics / Quality) — under the Built-in or HD pipelines the materials
render magenta.

## Using the prefabs

Drag any prefab from `Prefabs/URP/` into your scene. Each prop's pivot is placed at its
natural contact point (floor-sitting props are centered at floor height; wall-mounted props
are centered on their back face; grid modules pivot at their back-left bottom corner), so it
drops in at the right position with no manual offset and rotates in place.

### Modular / tiling pieces

Some assets are built as repeatable modules on a grid rather than one fixed mesh:

- **Pallet racking** — place a `PalletRack_StartBay`, then add `PalletRack_AddOnBay`
  instances at the 2700 mm bay pitch to extend the run. Each add-on bay reuses its
  neighbour's upright. Uprights and beam levels are separate named objects, so you change
  the rack's height or depth by adding or removing levels.
- **Boltless Shelving Unit** — a run is just N units duplicated along their width
  (900 mm pitch). There is no separate "shelving run" prefab.
- **Floor-Line Kit** — the straight / corner / T / cross / end-cap / hatch pieces snap to
  the grid and sit a millimetre or two above the floor; assemble aisle markings from them.
- **Sectional Overhead Door** — the door panels are separate objects tracked upward from
  the opening, so you can animate the door rolling up.

## Sample scene

`Samples/URP/URPScene.unity` — a single pick → pack → ship loop that demonstrates every
free-tier asset together, lit for URP. It also carries a `_Preview/Trailer Camera` with a
baked eye-level walkthrough animation (disabled by default).

Import via **Window → Package Manager → Warehouse Pack → Samples**, or open the scene
directly from the path above.

## Troubleshooting

**Materials render magenta / pink** — the project isn't on the Universal Render Pipeline, or
no URP asset is assigned in Project Settings → Graphics. This pack ships URP materials only.

**A prefab drops in below the floor** — check that your floor is at world Y 0. Floor-sitting
props pivot at their contact height, so they sit correctly on a surface at Y 0.
