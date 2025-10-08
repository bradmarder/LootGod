import { useState, useEffect } from 'react';
import { Alert, Button, Table } from 'react-bootstrap';
import axios from 'axios';
import classes from './eqClasses';

export default function LootRequests(props: IContext) {

	const [loading, setLoading] = useState(true);
	const [playerId, setPlayerId] = useState(0);

	useEffect(() => {
		axios
			.get<number>('/GetPlayerId')
			.then(x => setPlayerId(x.data))
			.finally(() => setLoading(false));
	}, []);

	const deleteLootRequest = (id: number) => {
		setLoading(true);
		axios
			.delete('/DeleteLootRequest', { params: { id }})
			.finally(() => setLoading(false));
	};

	const myLootRequests = props.requests.filter(x => x.playerId === playerId);

	return (
		<>
			<h3>My Loot Requests</h3>
			{myLootRequests.length === 0 &&
				<Alert variant='warning'>You have zero loot requests</Alert>
			}
			{myLootRequests.length > 0 &&
				<Table striped bordered hover size="sm">
					<thead>
						<tr>
							<th>Name</th>
							<th>Alt/Main</th>
							<th>Class</th>
							<th>Loot</th>
							<th>Quantity</th>
							<th>RaidNight / Rot</th>
							<th></th>
						</tr>
					</thead>
					<tbody>
						{myLootRequests.map(item =>
							<tr key={item.id}>
								<td>{item.altName || item.mainName}</td>
								<td>{item.isAlt ? 'Alt' : 'Main'}</td>
								<td>{classes[item.class]}</td>
								<td>{item.lootName}</td>
								<td>{item.spell || item.quantity}</td>
								<td>{item.raidNight ? 'RAID' : 'ROT'}</td>
								<td>
									{props.lootLocked &&
										<Alert variant={'danger'}>Loot Locked</Alert>
									}
									{!props.lootLocked &&
										<Button variant='danger' size='sm' disabled={loading} onClick={() => deleteLootRequest(item.id)}>Delete</Button>
									}
								</td>
							</tr>
						)}
					</tbody>
				</Table>
			}
		</>
	);
}