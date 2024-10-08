declare interface ILoot {
	readonly itemId: number
	readonly name: string;
	readonly raidQuantity: number;
	readonly rotQuantity: number;
	readonly isSpell: boolean;
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
