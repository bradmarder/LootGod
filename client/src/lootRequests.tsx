import { useState, useMemo } from 'react';
import './App.css';
import { Alert, Button, Table } from 'react-bootstrap';
import 'bootstrap/dist/css/bootstrap.min.css';
import axios from 'axios';
import classes from './eqClasses';

const api = process.env.REACT_APP_API_PATH;

export function LootRequests(props: IContext) {

	const [isLoading, setIsLoading] = useState(false);

	const deleteLootRequest = async (id: number) => {
		setIsLoading(true);
		try {
			await axios.post(api + '/DeleteLootRequest?id=' + id);
		}
		finally {
			setIsLoading(false);
		}
	};

	const myLootRequests = useMemo(() =>
		props.requests.filter(x => x.mainName.toLowerCase() === props.mainName.toLowerCase()),
		[props.mainName, props.requests]);

	return (
		<>
			<h3>My Loot Requests</h3>
			{myLootRequests.length === 0 &&
				<Alert variant='warning'>You currently have zero requests</Alert>
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
							<th></th>
						</tr>
					</thead>
					<tbody>
						{myLootRequests.map((item, i) =>
							<tr key={item.id}>
								<td>{item.characterName}</td>
								<td>{item.isAlt ? 'Alt' : 'Main'}</td>
								<td>{classes[item.class as any]}</td>
								<td>{props.loots.find(x => x.id === item.lootId)?.name}</td>
								<td>{item.spell || item.quantity}</td>
								<td>
									{props.lootLocked &&
										<Alert variant={'danger'}>Loot Locked</Alert>
									}
									{!props.lootLocked &&
										<Button variant='danger' disabled={isLoading} onClick={() => deleteLootRequest(item.id)}>Delete</Button>
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