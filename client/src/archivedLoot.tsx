import { useState } from 'react';
import { Row, Col, Button, Form, Table, FormCheck } from 'react-bootstrap';
import axios from 'axios';
import classes from './eqClasses';

export default function ArchivedLoot(props: IContext) {

	const [isLoading, setIsLoading] = useState(false);
	const [granted, setGranted] = useState(false);
	const [requests, setRequests] = useState<ILootRequest[]>([]);
	const [name, setName] = useState('');
	const [lootId, setLootId] = useState('');

	const getLootRequests = () => {
		setIsLoading(true);
		const params = {
			name: name || null,
			lootId: lootId || null,
		};
		axios
			.get<ILootRequest[]>('/GetArchivedLootRequests', { params })
			.then(res => setRequests(res.data))
			.finally(() => setIsLoading(false));
	};

	return (
		<>
			<h3>Archived Loot Requests (Admin Only)</h3>
			<Form onSubmit={e => { e.preventDefault(); }}>
				<Row>
					<Col>
						<Form.Group className="mb-3">
							<Form.Label>Character Name</Form.Label>
							<Form.Control type="text" placeholder="Enter name" value={name} onChange={e => setName(e.target.value)} />
						</Form.Group>
						<FormCheck id='reqForLabelClick2'>
							<FormCheck.Input checked={granted} onChange={e => setGranted(e.target.checked)} />
							<FormCheck.Label>Show only granted requests</FormCheck.Label>
						</FormCheck>
					</Col>
					<Col>
						<Form.Group>
							<Form.Label>Name</Form.Label>
							<Form.Select value={lootId} onChange={e => setLootId(e.target.value)}>
								<option value=''>Select Loot</option>
								{props.items.map(item =>
									<option key={item.id} value={item.id}>{item.name}</option>
								)}
							</Form.Select>
						</Form.Group>
					</Col>
				</Row>
				<Button variant='success' disabled={isLoading || (name === '' && lootId === '')} onClick={getLootRequests}>Search</Button>
			</Form>
			{requests.length > 0 &&
				<Table striped bordered hover size="sm">
					<thead>
						<tr>
							<th>Name</th>
							<th>Alt/Main</th>
							<th>Class</th>
							<th>Loot</th>
							<th>Quantity/Upgrading To</th>
							<th>Upgrading From</th>
							<th>Granted?</th>
							<th>Date</th>
							<th>RaidNight / Rot</th>
						</tr>
					</thead>
					<tbody>
						{requests.filter(x => !granted || x.granted).map((item) =>
							<tr key={item.id}>
								<td>{item.altName || item.mainName}</td>
								<td>{item.isAlt ? 'Alt' : 'Main'}</td>
								<td>{classes[item.class]}</td>
								<td>{item.lootName}</td>
								<td>{item.spell || item.quantity}</td>
								<td>{item.currentItem}</td>
								<td className={item.granted ? 'text-success' : 'text-danger'}>{item.granted ? "yes" : "no"}</td>
								<td>{new Date(item.createdDate + '+00:00').toLocaleDateString()}</td>
								<td>{item.raidNight ? 'Raid' : 'Rot'}</td>
							</tr>
						)}
					</tbody>
				</Table>
			}
		</>
	);
}