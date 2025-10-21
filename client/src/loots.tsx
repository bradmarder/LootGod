import { useRef, useState } from 'react';
import { Alert, Button, Accordion, OverlayTrigger } from 'react-bootstrap';
import axios from 'axios';
import classes from './eqClasses';
import ItemView from './item';

export default function Loots(props: IContext) {

	const ref = useRef(null);
	const [loading, setLoading] = useState(false);

	const grantLootRequest = (id: number, grant: boolean) => {
		setLoading(true);
		axios
			.post('/GrantLootRequest', { id, grant })
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

	const lootRequests = props.requests.filter(x => props.raidNight === x.raidNight);

	const loots = props.loots
		.filter(x => (props.raidNight ? x.raidQuantity : x.rotQuantity) > 0)
		.filter(x => x.item.isSpell === props.spell)

		// subtract the granted loot quantity from the total loot quantity
		.map(item => {
			const grantedLootQty = lootRequests
				.filter(x => x.itemId === item.itemId)
				.filter(x => x.granted)
				.map(x => x.quantity)
				.reduce((x, y) => x + y, 0);
			const availableQuantity = (props.raidNight ? item.raidQuantity : item.rotQuantity) - grantedLootQty;

			return { ...item, availableQuantity };
		});

	const getText = (request: ILootRequest) => [
		props.raidNight ? '' // raid night assumes main only
			: request.isAlt ? 'alt'
			: request.persona ? 'persona'
			: 'main',
		request.altName,
		classes[request.class],
		request.spell,
		request.currentItem,
	]
		.filter(x => x)
		.join(' | ');

	return (
		<>
			<div ref={ref} className='position-fixed top-50 start-0 translate-middle-y' style={{border: '3px solid orange', borderRadius: '5px'}}></div>
			<h3>Available {(props.spell ? 'Spells' : 'Loots')}</h3>
			{loots.length === 0 &&
				<Alert variant='warning'>
					There are zero {(props.spell ? 'spells' : 'loots')} available right now
				</Alert>
			}
			{loots.length > 0 &&
				<Accordion>
					{loots.map((loot, i) =>
						<Accordion.Item key={loot.itemId} eventKey={i.toString()}>
							<Accordion.Header>
								{!props.spell &&
									<OverlayTrigger key={loot.itemId} container={ref} overlay={() => ItemView(loot.item)}>
										<span className='text-warning font-monospace' style={{ whiteSpace: 'pre' }}>{loot.item.name.padEnd(35)}</span>
									</OverlayTrigger>
								}
								{props.spell &&
									<span className='text-warning font-monospace' style={{ whiteSpace: 'pre' }}>{loot.item.name.padEnd(35)}</span>
								}
								{loot.availableQuantity} available
								| {lootRequests.filter(x => x.itemId === loot.itemId && x.granted).length} granted
								| {lootRequests.filter(x => x.itemId === loot.itemId).length} request(s)
							</Accordion.Header>
							<Accordion.Body>
								{props.isAdmin &&
									<>
										<Button variant={'warning'} size={'sm'} disabled={loading} onClick={() => updateLootQuantity(loot.itemId, 1)}>Increment Quantity</Button>
										<Button variant={'danger'} size={'sm'} disabled={loading || loot.availableQuantity === 0} className='float-end' onClick={() => updateLootQuantity(loot.itemId, -1)}>Decrement Quantity</Button>
										<br /><br />
										{lootRequests.filter(x => x.itemId === loot.itemId).map(request =>
											<div key={request.id} className='font-monospace' style={{ whiteSpace: 'pre' }}>
												{request.granted &&
													<Button variant={'danger'} size={'sm'} disabled={loading} onClick={() => grantLootRequest(request.id, false)}>Ungrant</Button>
												}
												{!request.granted &&
													<Button variant={'success'} size={'sm'} disabled={loading || loot.availableQuantity === 0} onClick={() => grantLootRequest(request.id, true)}> Grant </Button>
												}
												<span className='text-primary'>   {request.mainName.padEnd(13)}</span>
												<span className={request.granted ? 'text-success' : ''}>
													{getText(request)}
												</span>
												{request.duplicate && !loot.item.isSpell &&
													<span className='text-danger'> | Duplicate Request?!</span>
												}
											</div>
										)}
										{loot.availableQuantity === 0 &&
											<>
												<br />
												<Alert variant={'warning'}><strong>Grant Disabled</strong> - Already Allotted Maximum Quantity</Alert>
											</>
										}
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
