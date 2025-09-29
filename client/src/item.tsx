const statStyle3 = { color: "#ffcc66" };
const statStyle4 = { color: "#ff6666" };
const luckColor = {color: "#00FFFF" };

function StatRow({ label, value, valueStyle, labelStyle }: { label: string; value: React.ReactNode; valueStyle?: React.CSSProperties, labelStyle?: React.CSSProperties }) {
	return (
		<div style={{ display: "flex", justifyContent: "space-between", minWidth: 130 }}>
			<span style={labelStyle}>{label}</span>
			<span style={valueStyle}>{value}</span>
		</div>
	);
}
const classMap = {
	WAR: 1,
	CLR: 1 << 1,
	PAL : 1 << 2,
	RNG : 1 << 3,
	SHD : 1 << 4,
	DRU : 1 << 5,
	MNK : 1 << 6,
	BRD : 1 << 7,
	ROG : 1 << 8,
	SHM : 1 << 9,
	NEC : 1 << 10,
	WIZ : 1 << 11,
	MAG : 1 << 12,
	ENC : 1 << 13,
	BST : 1 << 14,
	BER : 1 << 15,
};
const slotMap = {
	Charm: 1,
	Head: 1 << 2,
	Face: 1 << 3,
	Ear: 18,
	Neck: 1 << 5,
	Shoulders: 1 << 6,
	Arms: 1 << 7,
	Back: 1 << 8,
	Wrist: 1536,
	Fingers: 98304,
	Range: 1 << 11,
	Hands: 1 << 12,
	Primary: 1 << 13,
	Secondary: 16384,
	Waist: 1048576,
	Chest: 1 << 17,
	Legs: 1 << 18,
	Feet: 1 << 19,
};
const itemTypeMap = {
	0: '1H Slashing',
	1: '2H Slashing',
	2: '1H Piercing',
	3: '1H Blunt',
	4: '2H Blunt',
	5: 'Archery',
	35: '2H Piercing',
	45: 'Hand to Hand',
} as Record<number, string>;

function getClasses(flags: number) {
	if (flags === 65535) { return 'ALL'; }

	return Object
		.entries(classMap)
		.filter(x => (flags & x[1]) === x[1])
		.map(x => x[0])
		.join(' ');
}
function getSlot(flags: number) {
	return Object
		.entries(slotMap)
		.filter(x => (flags & x[1]) === x[1])
		.map(x => x[0])
		.join(' ');
}

export default function ItemView(item: ILoot) {
	return (
		<div
			style={{
				width: 500,
				height: 600, // base upon effect count?
				background: "linear-gradient(180deg, #23253a 0%, #181a28 100%)",
				color: "#fff",
				fontFamily: "Tahoma, Verdana, Geneva, sans-serif",
				fontSize: 16,
				padding: 16,
				boxSizing: "border-box",
				position: "relative",
			}}
		>
			<div style={{ display: "flex", alignItems: "flex-start" }}>
				<img src={`https://items.sodeq.org/img/item_${item.icon}.png`} width="40" height="40" />
				<div>
					<div style={{ color: "green", fontWeight: "bold", fontSize: 20 }}>
						{item.name}
					</div>
					<div>Lore, Prestige, Placeable</div>
					<div>Class: {getClasses(item.classes)}</div>
					<div>Race: ALL</div>
					<div>{getSlot(item.slots)}</div>
				</div>
			</div>
			{/* <div style={{ margin: "16px 0 8px 0" }}>
				<span style={{ color: "#bba85a" }}>
					Slot 5, type 20 (Weapon Ornamentation):{" "}
				</span>
				<span style={{ color: "#cccccc" }}>empty</span>
			</div> */}
			<div style={{ display: "flex", gap: 16 }}>
				<div style={{ minWidth: 130 }}>
					<StatRow label="Size:" value={<span style={{ color: "#cccccc" }}>HUGE</span>} />
					<StatRow label="Weight:" value={<span style={{ color: "#cccccc" }}>0</span>} />
					<StatRow label="Tribute:" value={<span style={{ color: "#cccccc" }}>0</span>} />
					{item.recLevel > 0 &&
						<StatRow label="Rec Level:" value={<span style={{ color: "#cccccc" }}>{item.recLevel}</span>} />
					}
					{item.reqLevel > 0 &&
						<StatRow label="Req Level:" value={<span style={{ color: "#cccccc" }}>{item.reqLevel}</span>} />
					}
					<StatRow label="Skill:" value={<span style={{ color: "#cccccc" }}>{itemTypeMap[item.itemtype]}</span>} />
				</div>
				<div style={{ minWidth: 120 }}>
					<StatRow label="AC:" value={<span style={{ color: "#cccccc" }}>{item.ac}</span>} />
					<StatRow label="HP:" value={<span style={{ color: "#cccccc" }}>{item.hp}</span>} />
					<StatRow label="Mana:" value={<span style={{ color: "#cccccc" }}>{item.mana}</span>} />
					<StatRow label="End:" value={<span style={{ color: "#cccccc" }}>{item.endurance}</span>} />
				</div>
				<div style={{ minWidth: 120 }}>
					{item.damage > 0 &&
						<>
							<StatRow label="Base Dmg:" value={<span style={{ color: "#cccccc" }}>{item.damage}</span>} />
							<StatRow label="Delay:" value={<span style={{ color: "#cccccc" }}>{item.delay}</span>} />
							<StatRow label="Ratio:" value={<span style={statStyle4}>{item.delay ? (item.damage / item.delay).toFixed(2) : ''}</span>} />
						</>
					}
					{false && // RANGE MUST EXIST
						<StatRow label="Range:" value={<span style={{ color: "#cccccc" }}>0</span>} />
					}
				</div>
			</div>
			<div style={{ marginTop: 16, display: "flex", gap: 32 }}>
				<div style={{ minWidth: 130 }}>
					<StatRow label="Strength:" value={<><span>0 </span><span style={statStyle3}>+{item.hstr}</span></>} />
					<StatRow label="Stamina:" value={<><span>0 </span><span style={statStyle3}>+{item.hsta}</span></>} />
					<StatRow label="Intelligence:" value={<><span>0 </span><span style={statStyle3}>+{item.hint}</span></>} />
					<StatRow label="Wisdom:" value={<><span>0 </span><span style={statStyle3}>+{item.hwis}</span></>} />
					<StatRow label="Agility:" value={<><span>0 </span><span style={statStyle3}>+{item.hagi}</span></>} />
					<StatRow label="Dexterity:" value={<><span>0 </span><span style={statStyle3}>+{item.hdex}</span></>} />
					<StatRow label="Charisma:" value={<><span>0 </span><span style={statStyle3}>+{item.hcha}</span></>} />
				</div>
				<div style={{ minWidth: 120 }}>
					<StatRow label="Magic:" value={0} />
					<StatRow label="Fire:" value={0} />
					<StatRow label="Cold:" value={0} />
					<StatRow label="Disease:" value={0} />
					<StatRow label="Poison:" value={0} />
					<StatRow label="Corrupt:" value={0} />
				</div>
				<div style={{ minWidth: 120 }}>
					{item.minLuck > 0 &&
						<StatRow label="Luck:" labelStyle={luckColor} value={item.minLuck + '-' + item.maxLuck} />
					}
					<StatRow label="Attack:" value={item.attack} />
					<StatRow label="HP Regen:" value={item.regen} />
					<StatRow label="Mana Regen:" value={item.manaRegen} />
					<StatRow label="End Regen:" value={item.enduranceRegen} />
					<StatRow label="Heal Amount:" value={item.healAmt} />
					<StatRow label="Spell Dmg:" value={item.spellDmg} />
					<StatRow label="Clairvoyance:" value={item.clairvoyance} />
				</div>
			</div>
			<div style={{ marginTop: 16 }}>
				{item.clickName &&
					<div>Effect: {item.clickName} (Casting Time: ??)</div>
				}
				{item.procName &&
					<div>Effect: {item.procName} (Combat)</div>
				}
				{item.wornName &&
					<div>Effect: {item.wornName} (Worn)</div>
				}
				{item.focusName &&
					<div>Focus Effect: {item.focusName}</div>
				}
			</div>
		</div>
	);
}