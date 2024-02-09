import { useState } from 'react';
import { Row, Col, Alert, Button, Form } from 'react-bootstrap';
import axios from 'axios';

export default function NewLoot() {

	const [isLoading, setIsLoading] = useState(false);
	const [createLootName, setCreateLootName] = useState('');

	const createLoot = async () => {
		setIsLoading(true);
		try {
			await axios.post('/CreateLoot?name=' + createLootName);
		}
		finally {
			setIsLoading(false);
		}
		setCreateLootName('');
	};

	return (
		<Alert variant='primary'>
			<h4>Add New LS Loot (Admin only)</h4>
			<Form onSubmit={createLoot}>
				<Row>
					<Col>
						<Form.Group>
							<Form.Label>Name</Form.Label>
                            <Form.Control type="text" placeholder='Enter new LS Loot Name (NO TYPOS PLEASE)' value={createLootName} onChange={e => setCreateLootName(e.target.value)} />
						</Form.Group>
					</Col>
				</Row>
				<br />
				<Button variant='success' disabled={isLoading} onClick={createLoot}>Create</Button>
			</Form>
		</Alert>
	);
}