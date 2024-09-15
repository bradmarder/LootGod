import { useState, useEffect } from 'react';
import { Table, Button, FormCheck } from 'react-bootstrap';
import axios from 'axios';

export default function RaidAttendance(props: { isAdmin: boolean, cacheKey: number }) {

	const [loading, setLoading] = useState(true);
	const [filter75, setFilter75] = useState(false);
	const [ra, setRa] = useState<IRaidAttendance[]>([]);
	const [cache, setCache] = useState(0);

	const getTextColor = (ra: number) => {
		return ra >= 70 ? 'text-success'
			: ra >= 50 ? 'text-warning'
			: 'text-danger';
	};

	const toggleHidden = (name: string) => {
		setLoading(true);
		axios
			.post('/ToggleHiddenPlayer', { name })
			.then(() => setCache(x => x + 1));
	};

	const toggleAdmin = (name: string) => {
		setLoading(true);
		axios
			.post('/TogglePlayerAdmin', { name })
			.then(() => setCache(x => x + 1));
	};

	useEffect(() => {
		const ac = new AbortController();
		setLoading(true);
		axios
			.get<IRaidAttendance[]>('/GetPlayerAttendance', { signal: ac.signal })
			.then(x => setRa(x.data))
			.finally(() => setLoading(false));
		return () => ac.abort();	
	}, [props.cacheKey, cache]);

	return (
		<>
			<h3>Raid Attendance</h3>
			<FormCheck>
				<FormCheck.Input checked={true} disabled={true} />
				<FormCheck.Label>Show only players with *ANY* RA for past 180 days</FormCheck.Label>
			</FormCheck>
			<FormCheck id='reqForLabelClick'>
				<FormCheck.Input checked={filter75} onChange={e => setFilter75(e.target.checked)} />
				<FormCheck.Label>Show only players with 75%+ RA for past 30 days</FormCheck.Label>
			</FormCheck>
			<Table striped bordered hover size="sm">
				<thead>
					<tr>
						<th>Name</th>
						<th>Rank</th>
						<th>30 Days</th>
						<th>90 Days</th>
						<th>180 Days</th>
						{props.isAdmin &&
							<th>Hide/Show (Admin Only)</th>
						}
						{props.isAdmin &&
							<th>Enable/Disable Admin (Leader Only)</th>
						}
					</tr>
				</thead>
				<tbody>
					{ra.filter(x => props.isAdmin || !x.hidden).filter(x => !filter75 || x._30 >= 75).map(item =>
						<tr key={item.name}>
							<td>{item.name}</td>
							<td>{item.rank}</td>
							<td className={getTextColor(item._30)}>{item._30}%</td>
							<td className={getTextColor(item._90)}>{item._90}%</td>
							<td className={getTextColor(item._180)}>{item._180}%</td>
							{props.isAdmin &&
								<td>
									{item.hidden &&
										<Button variant='warning' disabled={loading} onClick={() => toggleHidden(item.name)}>Show</Button>
									}
									{item.hidden === false &&
										<Button variant='success' disabled={loading} onClick={() => toggleHidden(item.name)}>Hide</Button>
									}
								</td>
							}
							{props.isAdmin &&
								<td>
									{!item.admin &&
										<Button variant='success' disabled={loading} onClick={() => toggleAdmin(item.name)}>Enable</Button>
									}
									{item.admin &&
										<Button variant='danger' disabled={loading} onClick={() => toggleAdmin(item.name)}>Disable</Button>
									}
								</td>
							}
						</tr>
					)}
				</tbody>
			</Table>
		</>
	);
}