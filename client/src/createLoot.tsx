import { useState } from 'react';
import './App.css';
import { Row, Col, Alert, Button, Form } from 'react-bootstrap';
import 'bootstrap/dist/css/bootstrap.min.css';
import axios, { AxiosPromise } from 'axios';

export function CreateLoot(props: { loots: ILoot[] }) {

	const [isLoading, setIsLoading] = useState(false);
	const [createLootName, setCreateLootName] = useState('');
	const [createLootQuantity, setCreateLootQuantity] = useState(1);

	const createLoot = async () => {
		const data = {
			Name: createLootName,
			Quantity: createLootQuantity,
		};
		setIsLoading(true);
		try {
			await axios.post<{}, AxiosPromise<ILoot>>('/UpdateLootQuantity', data);
		}
		finally {
			setIsLoading(false);
		}
		setCreateLootName('');
		setCreateLootQuantity(1);
	};

	return (
		<Alert variant='primary'>
			<h4>Create Loot (Admin only)</h4>
			<Form onSubmit={createLoot}>
				<Row>
					<Col>
						<Form.Group>
							<Form.Label>Name</Form.Label>
							<Form.Select value={createLootName} onChange={e => setCreateLootName(e.target.value)}>
								<option>Select Loot</option>
								{props.loots.map((item, i) =>
									<option key={item.id} value={item.name}>{item.name}</option>
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
				<Button variant='success' disabled={isLoading} onClick={createLoot}>Create</Button>
			</Form>
		</Alert>
	);
}