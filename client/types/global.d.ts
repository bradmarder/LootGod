declare interface ILoot {
	readonly id: number;
	readonly name: string;
	readonly raidQuantity: number;
	readonly rotQuantity: number;
	readonly isSpell: boolean;
}
declare interface ILootRequest {
	readonly id: number;
	readonly playerId: number;
	readonly createdDate: string;
	readonly mainName: string;
	readonly altName: string;
	readonly spell: string | null;
	readonly class: EQClass;
	readonly lootId: number;
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
	readonly loots: ReadonlyArray<ILoot>;
	readonly requests: ReadonlyArray<ILootRequest>;
	readonly spell?: boolean;
	readonly lootLocked?: boolean;
	readonly linkedAltsCacheKey?: number;
}
declare interface ILootLock {
	readonly lock: boolean;
	readonly createdDate: string | null;
}
declare interface IRaidAttendance {
	readonly name: string;
	readonly rank: string;
	readonly hidden: boolean;
	readonly admin: boolean;
	readonly _30: number;
	readonly _90: number;
	readonly _180: number;
}
