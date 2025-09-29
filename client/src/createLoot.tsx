import { useState } from 'react';
import { Row, Col, Alert, Button, Form, Spinner } from 'react-bootstrap';
import axios from 'axios';
import Swal from 'sweetalert2';

export default function CreateLoot(props: { items: IItem[], raidNight: boolean }) {

	const [loading, setLoading] = useState(false);
	const [createItemId, setCreateItemId] = useState(0);
	const [createLootQuantity, setCreateLootQuantity] = useState(1);

	const createLoot = () => {
		const data = {
			ItemId: createItemId,
			Quantity: createLootQuantity,
			RaidNight: props.raidNight,
		};
		setLoading(true);
		axios
			.post('/UpdateLootQuantity', data)
			.then(() => {
				setCreateItemId(0);
				setCreateLootQuantity(1);
			})
			.finally(() => setLoading(false));
	};
	const syncItems = () => {
		setLoading(true);
		axios
			.post('/ItemSync')
			.then(() => {
				Swal.fire('Item Sync Success', `Successfully synced items`, 'success');
			})
			.finally(() => setLoading(false));
	};
	const syncSpells = () => {
		setLoading(true);
		axios
			.post('/SpellSync')
			.then(() => {
				Swal.fire('Spell Sync Success', `Successfully synced spells`, 'success');
			})
			.finally(() => setLoading(false));
	};

	return (
		<Alert variant='primary'>
			<h4>Create Loot (Admin only)</h4>
			<Form onSubmit={createLoot}>
				<Row>
					<Col>
						<Form.Group>
							<Form.Label>Name</Form.Label>
							<Form.Select value={createItemId} onChange={e => setCreateItemId(Number(e.target.value))}>
								<option value={0}>Select Loot</option>
								{props.items.map(item =>
									<option key={item.id} value={item.id}>{item.name}</option>
								)}
							</Form.Select>
						</Form.Group>
					</Col>
					<Col>
						<Form.Group>
							<Form.Label>Quantity</Form.Label>
							<Form.Control type="number" placeholder="Quantity" min="1" max="255" value={createLootQuantity} onChange={e => setCreateLootQuantity(Number(e.target.value))} />
						</Form.Group>
					</Col>
				</Row>
				<br />
				<Button variant='success' disabled={loading || createItemId === 0} onClick={createLoot}>Create</Button>
				<Button variant='warning' disabled={loading} onClick={syncItems} className='float-end'>
					Sync Items
					{loading &&
						<Spinner animation="border" role="status">
							<span className="visually-hidden">Loading...</span>
						</Spinner>
					}
				</Button>
				<Button variant='warning' disabled={loading} onClick={syncSpells} className='float-end'>
					Sync Spells
					{loading &&
						<Spinner animation="border" role="status">
							<span className="visually-hidden">Loading...</span>
						</Spinner>
					}
				</Button>
			</Form>
		</Alert>
	);
}