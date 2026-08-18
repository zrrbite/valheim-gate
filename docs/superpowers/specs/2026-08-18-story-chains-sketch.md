# Run Mode: story chains — design sketch (pre-brainstorm)

Martin's brief (2026-08-18): challenges should tell the story of a
character's natural journey — build axe, chop wood, build home, kill
animals, roast meat — extended per biome; some quests grant ITEMS
(armor), like epic quests.

Reward economy: story beats → heat; chain completion → boon offer;
epic quests → item grant (via existing ISpawnService, mechanically ready).
Items = progression skips, consistent with the empowerment pillar.

Slot model: slot 1 = story slot (next spine beat), slots 2-3 = random
micro-quests; epics as a long-running fourth banner quest, one per act.

Acts and beats: see the 2026-08-18 conversation overview (Acts I-V:
Meadows/Black Forest/Swamp/Mountain/Plains, each with spine beats, one
epic, detection notes). All beats use existing detection (kill hook,
defeat keys, inventory polling, PlayerStats: Sleep, FoodEaten, MineHits)
plus ONE new primitive: nearby player-built piece polling (shelter beat).

Status: sketch only — full brainstorm with Martin before building.
