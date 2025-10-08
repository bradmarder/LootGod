import { useState } from 'react';
import { Row, Col, Alert, Button, Form } from 'react-bootstrap';
import axios from 'axios';

export default function NewLoot() {

	const [loading, setLoading] = useState(false);
	const [createLootName, setCreateLootName] = useState('');

	const createItem = () => {
		setLoading(true);
		axios
			.post('/CreateItem?name=' + encodeURIComponent(createLootName))
			.then(() => setCreateLootName(''))
			.finally(() => setLoading(false));
	};

	return (
		<Alert variant='primary'>
			<h4>Add New Loot (Admin only)</h4>
			<Form onSubmit={createItem}>
				<Row>
					<Col>
						<Form.Group>
							<Form.Label>Name</Form.Label>
							<Form.Control type="text" placeholder='Enter new LS Loot Name (NO TYPOS PLEASE)' value={createLootName} onChange={e => setCreateLootName(e.target.value)} />
						</Form.Group>
					</Col>
				</Row>
				<br />
				<Button variant='success' disabled={loading || createLootName.length < 2} onClick={createItem}>Create</Button>
			</Form>
		</Alert>
	);
}