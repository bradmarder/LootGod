import { useState } from 'react';
import { Alert, Button, Table } from 'react-bootstrap';
import axios from 'axios';
import classes from './eqClasses';
import Swal from "sweetalert2/dist/sweetalert2.js";

export default function GrantedLoots(props: IContext) {

	const [loading, setLoading] = useState(false);

	const ungrantLootRequest = (id: number) => {
		setLoading(true);
		axios
			.post('/GrantLootRequest', { id, grant: false })
			.then(() => setLoading(false));
	};
	const grantedLootRequests = props.requests.filter(x => x.granted);
	const finishLootGranting = () => {
		setLoading(true);
		return Swal.fire({
			title: 'Confirmation',
			text: `Are you ready to finish granting ${grantedLootRequests.length} loot requests?`,
			icon: 'warning',
			showCancelButton: true,
			confirmButtonText: 'Yes, Finish',
		})
		.then(x => x.isConfirmed
			? axios.post('/FinishLootRequests', { raidNight: props.raidNight })
			: Promise.reject())
		.then(() => Swal.fire('Finished!', 'Successfully finished granting loot requests.', 'success'))
		.finally(() => setLoading(false));
	};

	const lootOutputHref = '/api/GetGrantedLootOutput?' + new URLSearchParams({
		playerKey: localStorage.getItem('key')!,
		raidNight: props.raidNight + '',
	}).toString();

	return (
		<>
			<h3>Granted Loot Requests (Admin Only)</h3>
			{grantedLootRequests.length === 0 &&
				<Alert variant='warning'>There are currently zero granted loot requests</Alert>
			}
			{grantedLootRequests.length > 0 &&
				<>
				<a target="_blank" rel="noreferrer" href={lootOutputHref}>Download Granted Loot Output Text File (for Discord)</a>
				<Alert variant={'warning'}>
					<strong>WARNING!</strong> This archives all active loot requests and resets quantities. If you have a Discord webhook set, this will also
					post the granted loot output to that channel.
					<br />
					<Button variant={'success'} disabled={loading} onClick={finishLootGranting}>Finish Granting Loots</Button>
				</Alert>
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
						{grantedLootRequests.map(item =>
							<tr key={item.id}>
								<td><strong>{item.mainName}</strong> - {item.altName}</td>
								<td>{item.isAlt ? 'Alt' : 'Main'}</td>
								<td>{classes[item.class]}</td>
								<td>{item.lootName}</td>
								<td>{item.spell || item.quantity}</td>
								<td>
									<Button variant='danger' disabled={loading} onClick={() => ungrantLootRequest(item.id)}>Ungrant</Button>
								</td>
							</tr>
						)}
					</tbody>
				</Table>
				</>
			}
		</>
	);
}