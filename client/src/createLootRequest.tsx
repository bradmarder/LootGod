import { useState, useEffect } from 'react';
import { Row, Col, Alert, Button, Form, FormCheck } from 'react-bootstrap';
import axios from 'axios';
import classes from './eqClasses';

export default function CreateLootRequest(props: IContext) {

	const [loading, setLoading] = useState(true);
	const [persona, setPersona] = useState(false);
	const [altName, setAltName] = useState('');
	const [spell, setSpell] = useState('');
	const [currentItem, setCurrentItem] = useState('');
	const [quantity, setQuantity] = useState(1);
	const [eqClass, setClass] = useState<EQClass | ''>('');
	const [itemId, setItemId] = useState(0);
	const [linkedAlts, setLinkedAlts] = useState<string[]>([]);

	const hasQtyLoots = props.loots.filter(x => (props.raidNight ? x.raidQuantity : x.rotQuantity) > 0);

	const spellSelected = itemId > 0 && props.loots.find(x => x.itemId === itemId)!.item.isSpell;
	const reset = () => {
		setAltName('');
		setItemId(0);
		setQuantity(1);
		setClass('');
		setSpell('');
		setCurrentItem('');
		setLoading(false);
	};

	const isCreateLootDisabled =
		hasQtyLoots.length === 0
		|| loading
		|| itemId === 0
		|| quantity < 1

		// if specifying an alt, then class is required
		|| (altName.length > 0 && eqClass === '')

		// if a spell is selected, the user *must* enter a spell name
		|| (spellSelected && spell == null);

	const createLootRequest = () => {
		const data = {
			AltName: altName || null,
			Class: eqClass === '' ? null : classes.indexOf(eqClass),
			ItemId: itemId,
			Quantity: spellSelected ? 1 : quantity,
			Spell: spellSelected ? spell : null,
			CurrentItem: currentItem,
			RaidNight: props.raidNight,
			Persona: persona,
		};
		setLoading(true);
		axios
			.post('/CreateLootRequest', data)
			.finally(() => reset());
	};
	const setLootLogic = (itemId: number) => {

		// if someone selects a spell, they must enter the name of the spell, and the quantity defaults to 1
		// ....but then we have to remove the char/lootId unique combo...
		if (itemId > 0 && props.loots.find(x => x.itemId === itemId)!.item.isSpell) {
			setQuantity(1);
		}

		setItemId(itemId);
	};

	useEffect(() => {
		const ac = new AbortController();
		setLoading(true);
		axios
			.get<string[]>('/GetLinkedAlts', { signal: ac.signal })
			.then(x => setLinkedAlts(x.data))
			.finally(() => setLoading(false));
		return () => ac.abort();
	}, [props.linkedAltsCacheKey]);

	return (
		<Alert variant='primary'>
			<h4>Create Loot Request</h4>
			<Form onSubmit={e => { e.preventDefault(); }}>
				{!props.raidNight &&
					<Row>
						<Col xs={12} md={6}>
							<Form.Group className="mb-3">
								<Form.Label>Main / Alt (Alts must be guild tagged and linked to you)</Form.Label>
								<Form.Select value={altName} onChange={e => setAltName(e.target.value)}>
									<option value=''>Main</option>
									{linkedAlts.map(name =>
										<option key={name} value={name}>{name}</option>
									)}
								</Form.Select>
							</Form.Group>
						</Col>
						<Col xs={12} md={6}>
							<Form.Group className="mb-3">
								<Form.Label>Class</Form.Label>
								<Form.Select value={eqClass} onChange={e => setClass(e.target.value as EQClass)}>
									<option>Select Class</option>
									{classes.map(item =>
										<option key={item} value={item}>{item}</option>
									)}
								</Form.Select>
							</Form.Group>
						</Col>
					</Row>
				}
				{!props.raidNight &&
					<Row>
						<Col xs={12} md={6}>
							<FormCheck>
								<FormCheck.Input checked={persona} onChange={e => setPersona(e.target.checked)} />
								<FormCheck.Label>Persona</FormCheck.Label>
							</FormCheck>
						</Col>
					</Row>
				}
				<hr />
				<Row>
					<Col xs={12} md={6}>
						<Form.Group className="mb-3">
							<Form.Label>Loot</Form.Label>
							<Form.Select value={itemId} onChange={e => setLootLogic(Number(e.target.value))}>
								<option value={0}>Select an Item</option>
								{hasQtyLoots.map(loot =>
									<option key={loot.itemId} value={loot.itemId}>{loot.item.name}</option>
								)}
							</Form.Select>
						</Form.Group>
					</Col>
					<Col xs={12} md={6}>
						<Form.Group className="mb-3">
							<Form.Label>Quantity</Form.Label>
							<Form.Control type="number" disabled={spellSelected || true} placeholder="Quantity" min="1" max="255" value={quantity} onChange={e => setQuantity(Number(e.target.value))} />
						</Form.Group>
					</Col>
				</Row>
				{spellSelected &&
					<Row>
						<Col>
							<Form.Group className="mb-3">
								<Form.Label>Looks like you've selected a spell rune/nugget. You are <strong className={'text-danger'}>*required*</strong> to name the spell/item you want with this rune/nugget.</Form.Label>
								<Form.Control type="text" placeholder="Spell name" value={spell} onChange={e => setSpell(e.target.value)} />
							</Form.Group>
						</Col>
					</Row>
				}
				<Row>
					<Col>
						<Form.Group className="mb-3">
							<Form.Label>Upgrading From (your current item) <strong className={'text-danger'} hidden={!props.raidNight}>Required</strong></Form.Label>
							<Form.Control type="text" placeholder="Current Item" value={currentItem} onChange={e => setCurrentItem(e.target.value)} />
						</Form.Group>
					</Col>
				</Row>
				{props.lootLocked &&
					<Alert variant={'danger'}>
						Loot requests are currently locked/disabled. Please check back later!
					</Alert>
				}
				{!props.lootLocked &&
					<Button variant='success' disabled={isCreateLootDisabled} onClick={createLootRequest}>Create</Button>
				}
			</Form>
		</Alert>
	);
}