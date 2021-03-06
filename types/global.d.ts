declare interface ILoot {
    readonly id: number;
    readonly name: string;
    quantity: number;
    readonly isSpell: boolean;
}
declare interface ILootRequest {
    readonly id: number;
    readonly createdDate: string;
    readonly mainName: string;
    readonly characterName: string;
    readonly spell: string | null;
    readonly class: EQClass;
    readonly lootId: number;
    readonly quantity: number;
    readonly isAlt: boolean;
    readonly granted: boolean;
}
declare interface IContext {
    readonly mainName: string;
    readonly isAdmin: boolean;
    readonly loots: ReadonlyArray<ILoot>;
    readonly requests: ReadonlyArray<ILootRequest>;
    readonly spell?: boolean;
    readonly lootLocked?: boolean;
}
declare interface ILootLock {
    readonly lock: boolean;
    readonly createdDate: string | null;
}
