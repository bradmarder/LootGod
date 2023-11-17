import { useState, useEffect } from 'react';
import { Row, Col, Alert, Button, Form } from 'react-bootstrap';
import axios from 'axios';
import classes from './eqClasses';
import { EQClass } from './eqClass';

export default function CreateLootRequest(props: IContext) {

	const [isLoading, setIsLoading] = useState(true);
	const [altName, setAltName] = useState('');
	const [spell, setSpell] = useState('');
	const [currentItem, setCurrentItem] = useState('');
	const [quantity, setQuantity] = useState(1);
	const [eqClass, setClass] = useState('');
	const [lootId, setLootId] = useState(0);
	const [linkedAlts, setLinkedAlts] = useState<string[]>([]);

	const hasQtyLoots = props.loots.filter(x => (props.raidNight ? x.raidQuantity : x.rotQuantity) > 0);

	const spellSelected = lootId > 0 && props.loots.filter(x => x.id === lootId)[0]!.isSpell;

	const isCreateLootDisabled =
		hasQtyLoots.length === 0
		|| isLoading
		|| lootId === 0
		|| quantity < 1

		// if specifying an alt, then class is required
		|| (altName.length > 0 && eqClass === '')

		// if a spell is selected, the user *must* enter a spell name
		|| (spellSelected && spell == null);

	const createLootRequest = async () => {
		const data = {
			AltName: altName || null,
			Class: altName === '' ? null : classes.indexOf(eqClass as EQClass),
			LootId: lootId,
			Quantity: spellSelected ? 1 : quantity,
			Spell: spellSelected ? spell : null,
			CurrentItem: currentItem,
			RaidNight: props.raidNight,
		};
		setIsLoading(true);
		try {
			await axios.post('/CreateLootRequest', data);
		}
		catch {
			// DUPLICATE REQUEST ERROR MESSAGE
			alert('duplicate request for name and loot');
		}
		finally {
			setIsLoading(false);
		}
		setAltName('');
		setLootId(0);
		setQuantity(1);
		setClass('');
		setSpell('');
		setCurrentItem('');
	};
	const setLootLogic = (lootId: number) => {

		// if someone selects a spell, they must enter the name of the spell, and the quantity defaults to 
		// ....but then we have to remove the char/lootId unique combo...
		if (lootId > 0 && props.loots.filter(x => x.id === lootId)[0]!.isSpell) {
			setQuantity(1);
		}

		setLootId(lootId);
	};
	const loadLinkedAlts = () => {
        axios
            .get<string[]>('/GetLinkedAlts')
            .then(x => setLinkedAlts(x.data))
            .finally(() => setIsLoading(false));
    };
    useEffect(loadLinkedAlts, [props.linkedAltsCacheKey]);

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
								<Form.Select value={eqClass} onChange={e => setClass(e.target.value)}>
									<option>Select Class</option>
									{classes.map(item =>
										<option key={item} value={item}>{item}</option>
									)}
								</Form.Select>
							</Form.Group>
						</Col>
					</Row>
				}
				<Row>
					<Col xs={12} md={6}>
						<Form.Group className="mb-3">
							<Form.Label>Loot</Form.Label>
							<Form.Select value={lootId} onChange={e => setLootLogic(Number(e.target.value))}>
								<option value={0}>Select an Item</option>
								{hasQtyLoots.map(item =>
									<option key={item.id} value={item.id}>{item.name}</option>
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