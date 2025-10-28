import { useState } from 'react';
import { Row, Col, Alert, Button, Form, Spinner } from 'react-bootstrap';
import axios from 'axios';
import swal from './swal';

export default function CreateLoot(props: { items: IItemSearch[], raidNight: boolean }) {

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
	const syncData = () => {
		setLoading(true);
		axios
			.post('/DataSync')
			.then(() => {
				swal.fire('Data Sync Success', `Successfully synced items`, 'success');
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
				<Button variant='warning' disabled={loading} onClick={syncData} className='float-end'>
					Sync Spells/Items
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