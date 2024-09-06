import { useState } from 'react';
import { Alert, Button, Accordion } from 'react-bootstrap';
import axios from 'axios';
import classes from './eqClasses';

export default function Loots(props: IContext) {

	const [loading, setLoading] = useState(false);

	const grantLootRequest = (id: number) => {
		setLoading(true);
		axios
			.post('/GrantLootRequest?id=' + id + '&grant=true')
			.finally(() => setLoading(false));
	};

	const updateLootQuantity = (itemId: number, value: number) => {
		setLoading(true);
		const loot = props.loots.find(x => x.itemId === itemId)!;
		const quantity = value + (props.raidNight ? loot.raidQuantity : loot.rotQuantity);
		const dto = { itemId, quantity, raidNight: props.raidNight };
		axios
			.post('/UpdateLootQuantity', dto)
			.finally(() => setLoading(false));
	};

	const ungrantLootRequest = (id: number) => {
		setLoading(true);
		axios
			.post('/GrantLootRequest?id=' + id + '&grant=false')
			.finally(() => setLoading(false));
	};

	const lootRequests = props.requests.filter(x => props.raidNight === x.raidNight);

	const loots = props.loots
		.filter(x => (props.raidNight ? x.raidQuantity : x.rotQuantity) > 0)
		.filter(x => props.spell ? x.isSpell : !x.isSpell)

		// subtract the granted loot quantity from the total loot quantity
		.map(item => {
			const grantedLootQty = lootRequests
				.filter(x => x.itemId === item.itemId)
				.filter(x => x.granted)
				.map(x => x.quantity)
				.reduce((x, y) => x + y, 0);

			return props.raidNight
				? { ...item, raidQuantity: item.raidQuantity - grantedLootQty }
				: { ...item, rotQuantity: item.rotQuantity - grantedLootQty };
		});

	const getText = (req: ILootRequest) =>
		[
			req.mainName,
			req.altName,
			req.isAlt ? 'alt' : 'main',
			classes[req.class],
			req.spell || req.quantity,
			req.currentItem,
		].join(' | ');

	const getSpellLevel = (name: string) => {
		const level = name.startsWith('Glowing') ? 125
			: name.startsWith('Greater') ? 124
			: name.startsWith('Median') ? 123
			: name.startsWith('Lesser') ? 122
			: name.startsWith('Minor') ? 121
			: 255;
		return level + ' | ';
	};

	return (
		<>
			<h3>Available {(props.spell ? 'Spells/Nuggets' : 'Loots')}</h3>
			{loots.length === 0 &&
				<Alert variant='warning'>
					Looks like there aren't any {(props.spell ? 'spells/nuggets' : 'loots')} available right now
				</Alert>
			}
			{loots.length > 0 &&
				<Accordion>
					{loots.map((loot, i) =>
						<Accordion.Item key={loot.itemId} eventKey={i.toString()}>
							<Accordion.Header>
								{!props.spell &&
									<a href={'https://www.raidloot.com/items?view=List&name=' + loot.name} target='_blank' rel='noreferrer'>{loot.name}</a>
								}
								{props.spell && getSpellLevel(loot.name)}
								{props.spell && loot.name}
								&nbsp;| {props.raidNight ? loot.raidQuantity : loot.rotQuantity} available | {lootRequests.filter(x => x.itemId === loot.itemId && x.granted).length} granted | {lootRequests.filter(x => x.itemId === loot.itemId).length} request(s)
							</Accordion.Header>
							<Accordion.Body>
								{props.isAdmin &&
									<>
										{(props.raidNight ? loot.raidQuantity : loot.rotQuantity) === 0 &&
											<Alert variant={'warning'}><strong>Grant Disabled</strong> - Already Allotted Maximum Quantity</Alert>
										}
										<Button variant={'warning'} size={'sm'} disabled={loading} onClick={() => updateLootQuantity(loot.itemId, 1)}>Increment Quantity</Button>
										<Button variant={'danger'} size={'sm'} disabled={loading || (props.raidNight ? loot.raidQuantity : loot.rotQuantity) === 0} onClick={() => updateLootQuantity(loot.itemId, -1)}>Decrement Quantity</Button>
										<br /><br />
										{lootRequests.filter(x => x.itemId === loot.itemId).map(req =>
											<span key={req.id}>
												{getText(req)}
												&nbsp;
												{props.isAdmin && req.granted &&
													<Button variant={'danger'} disabled={loading} onClick={() => ungrantLootRequest(req.id)}>Un-Grant</Button>
												}
												{props.isAdmin && !req.granted && (props.raidNight ? loot.raidQuantity : loot.rotQuantity) > 0 &&
													<Button variant={'success'} disabled={loading} onClick={() => grantLootRequest(req.id)}>Grant</Button>
												}
												<br />
											</span>
										)}
									</>
								}
							</Accordion.Body>
						</Accordion.Item>
					)}
				</Accordion>
			}
		</>
	);
}
