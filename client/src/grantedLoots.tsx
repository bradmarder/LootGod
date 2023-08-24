import { useState, useMemo } from 'react';
import { Alert, Button, Table } from 'react-bootstrap';
import axios from 'axios';
import classes from './eqClasses';

export default function GrantedLoots(props: IContext) {

	const [isLoading, setIsLoading] = useState(false);

	const ungrantLootRequest = (id: number) => {
		setIsLoading(true);
		axios
			.post('/GrantLootRequest?id=' + id + '&grant=false')
			.then(() => setIsLoading(false));
	};
	const finishLootGranting =  () => {
		setIsLoading(true);
		axios
			.post('/FinishLootRequests?raidNight=' + props.raidNight)
			.then(() => setIsLoading(false));
	};

	const grantedLootRequests = useMemo(() =>
		props.requests.filter(x => x.granted),
		[props.requests]);

	return (
		<>
			<h3>Granted Loot Requests (Admin Only)</h3>
			{grantedLootRequests.length === 0 &&
				<Alert variant='warning'>There are currently zero granted loot requests</Alert>
			}
			{grantedLootRequests.length > 0 &&
				<>
					<a target="_blank" rel="noreferrer" href={'/api/GetGrantedLootOutput?playerKey=' + localStorage.getItem('key')}>Download Granted Loot Output Text File (for discord)</a>
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
							{grantedLootRequests.map((item, i) =>
								<tr key={item.id}>
									<td><strong>{item.mainName}</strong> - {item.altName}</td>
									<td>{item.isAlt ? 'Alt' : 'Main'}</td>
									<td>{classes[item.class]}</td>
									<td>{item.lootName}</td>
									<td>{item.spell || item.quantity}</td>
									<td>
										<Button variant='danger' disabled={isLoading} onClick={() => ungrantLootRequest(item.id)}>Ungrant</Button>
									</td>
								</tr>
							)}
						</tbody>
					</Table>
					<Alert variant={'warning'}>
						<strong>WARNING!</strong> This archives all active loot requests and resets quantities. If you have a Discord webhook set, this will also
						post the granted loot output to that channel.
						<br />
						<Button variant={'success'} disabled={isLoading} onClick={finishLootGranting}>Finish Granting Loots</Button>
					</Alert>
				</>
			}
		</>
	);
}