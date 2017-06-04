/*
Injects a new modded WorkTypeDef into pawns in an existing saved game.
Should be run each time a map is loaded. Will have no effect on pawns that already "know" the provided work type.

An injection is necessary, because the pawn work priority settings don't have the work type registered. This would produce exceptions when work givers would run or the Work tab is opened.

Dependencies: Harmony 1.0.9.1
*/

public static void EnsureAllColonistsKnowWorkType(WorkTypeDef def, Map map) {
	const int disabledWorkPriority = 0;
	try {
		var injectedPawns = new HashSet<Pawn>();
		if (map == null || map.mapPawns == null) return;
		foreach (var pawn in map.mapPawns.PawnsInFaction(Faction.OfPlayer)) {
			if (pawn == null || pawn.workSettings == null) continue;
			var workDefMap = Traverse.Create(pawn.workSettings).Field("priorities").GetValue<DefMap<WorkTypeDef, int>>();
			if (workDefMap == null) throw new Exception("Failed to retrieve workDefMap for pawn: " + pawn);
			var priorityList = Traverse.Create(workDefMap).Field("values").GetValue<List<int>>();
			if (priorityList == null) throw new Exception("Failed to retrieve priority list for pawn: " + pawn);
			if (priorityList.Count > 0) {
				var cyclesLeft = 100;
				// the priority list must be padded to accomodate our WorkTypeDef.index
				// the value added will be the priority for our work type
				// we need to ensure that all worktype indices in the database will fit in the list, not only or own
				var maxIndex = DefDatabase<WorkTypeDef>.AllDefs.Max(d => d.index);
				while (priorityList.Count <= maxIndex && cyclesLeft > 0) {
					cyclesLeft--;
					var nowAddingSpecifiedWorktype = priorityList.Count == maxIndex;
					int priority = disabledWorkPriority;
					if (nowAddingSpecifiedWorktype) {
						priority = GetWorkTypePriorityForPawn(def, pawn);
					}
					priorityList.Add(priority);
					injectedPawns.Add(pawn);
				}
				if (cyclesLeft == 0) {
					throw new Exception(string.Format("Ran out of cycles while trying to pad work priorities array:  {0} {1} {2} {3}", def.defName, pawn.Name, priorityList.Count, Environment.StackTrace));
				}
			}
		}
		if (injectedPawns.Count > 0) {
			Log.Message("Injected work type {0} into pawns: {1}", def.defName, injectedPawns.Join(", ", true));
		}
	} catch (Exception e) {
		Log.Error("Exception while injecting WorkTypeDef into colonist pawns: " + e);
	}
}

// returns a work priority based on disabled work types and tags for that pawn
private static int GetWorkTypePriorityForPawn(WorkTypeDef workDef, Pawn pawn) {
	const int disabledWorkPriority = 0;
	const int defaultWorkPriority = 3;
	if (pawn.story != null){
		if (pawn.story.WorkTypeIsDisabled(workDef) || pawn.story.WorkTagIsDisabled(workDef.workTags)) {
			return disabledWorkPriority;
		}
	}
	return defaultWorkPriority;
}