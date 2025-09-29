declare interface ILoot extends IItemStats {
	readonly itemId: number
	readonly name: string;
	readonly raidQuantity: number;
	readonly rotQuantity: number;
	readonly isSpell: boolean;

	/** value computed on the client */
	readonly availableQuantity: number;
}
declare interface IItemStats
{
	readonly sync: number;
	readonly hash: any;
	readonly expansion: number;
	readonly classes: number;
	readonly prestige: number;
	readonly slots: number;
	readonly regen: number;
	readonly manaRegen: number;
	readonly enduranceRegen: number;
	readonly healAmt: number;
	readonly spellDmg: number;
	readonly clairvoyance: number;
	readonly attack: number;
	readonly itemtype: number;
	readonly augslot1type: number;
	readonly augslot3type: number;
	readonly augslot4type: number;
	readonly stacksize: number;
	readonly hp: number;
	readonly mana: number;
	readonly endurance: number;
	readonly ac: number;
	readonly icon: number;
	readonly damage: number;
	readonly delay: number;
	readonly reqLevel: number;
	readonly recLevel: number;
	readonly hstr: number;
	readonly hint: number;
	readonly hwis: number;
	readonly hagi: number;
	readonly hdex: number;
	readonly hsta: number;
	readonly hcha: number;
	readonly minLuck: number;
	readonly maxLuck: number;
	readonly lore: number;
	readonly procLevel: number;
	readonly focusLevel: number;
	readonly procEffect: number;
	readonly focusEffect: number;
	readonly clickEffect: number;
	readonly wornEffect: number;
	readonly clickLevel: number;
	readonly wornName: string | null;
	readonly procName: string | null;
	readonly procDescription: string | null;
	readonly procDescription2: string | null;
	readonly clickName: string | null;
	readonly clickDescription: string | null;
	readonly clickDescription2: string | null;
	readonly focusName: string | null;
	readonly focusDescription: string | null;
	readonly focusDescription2: string | null;
}
declare interface IItem {
	readonly id: number;
	readonly name: string;
}
declare interface ILootRequest {
	readonly id: number;
	readonly playerId: number;
	readonly createdDate: number;
	readonly mainName: string;
	readonly altName: string;
	readonly spell: string | null;
	readonly class: EQClass;
	readonly itemId: number;
	readonly lootName: string;
	readonly quantity: number;
	readonly raidNight: boolean;
	readonly isAlt: boolean;
	readonly granted: boolean;
	readonly currentItem: string;
}
declare interface IContext {
	readonly raidNight: boolean;
	readonly isAdmin: boolean;
	readonly loots: readonly ILoot[];
	readonly items: readonly IItem[];
	readonly requests: readonly ILootRequest[];
	readonly spell?: boolean;
	readonly lootLocked?: boolean;
	readonly linkedAltsCacheKey?: number;
}
declare interface IRaidAttendance {
	readonly id: number;
	readonly name: string;
	readonly rank: string;
	readonly hidden: boolean;
	readonly admin: boolean;
	readonly _30: number;
	readonly _90: number;
	readonly _180: number;
	readonly lastOnDate: string | null;
	readonly notes: string | null;
	readonly zone: string | null;
	readonly class: EQClass;
	readonly level: number;
	readonly alts: readonly string[];
	readonly t1GrantedLootCount: number;
	readonly t2GrantedLootCount: number;
}
declare interface IDiscordWebhooks {
	readonly raid: string;
	readonly rot: string;
}
