import { useState, useEffect } from 'react';
import { Table, Button, FormCheck } from 'react-bootstrap';
import axios from 'axios';

export default function RaidAttendance(props: { isAdmin: boolean, cacheKey: number }) {

	const [isLoading, setIsLoading] = useState(true);
	const [filter75, setFilter75] = useState(false);
	const [ra, setRa] = useState<IRaidAttendance[]>([]);

	const getRA = () => {
		axios
			.get<IRaidAttendance[]>('/GetPlayerAttendance')
			.then(x => setRa(x.data))
			.finally(() => setIsLoading(false));
	};

	const getTextColor = (ra: number) => {
		return ra >= 70 ? 'text-success'
			: ra >= 50 ? 'text-warning'
			: 'text-danger';
	};

	const toggleHidden = (name: string) => {
		setIsLoading(true);
		axios
			.post('/ToggleHiddenPlayer?playerName=' + name)
			.then(getRA);
	};

	const toggleAdmin = (name: string) => {
		setIsLoading(true);
		axios
			.post('/TogglePlayerAdmin?playerName=' + name)
			.then(getRA);
	};

	useEffect(getRA, [props.cacheKey]);

	return (
		<>
			<h3>Raid Attendance</h3>
			<FormCheck id='reqForLabelClick'>
				<FormCheck.Input checked={filter75} onChange={e => setFilter75(e.target.checked)} />
				<FormCheck.Label>Show only players with 75%+ RA for past 30 days</FormCheck.Label>
			</FormCheck>
			<>
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
						{ra.filter(x => props.isAdmin ? true : !x.hidden).filter(x => !filter75 || x._30 >= 75).map((item, i) =>
							<tr key={item.name}>
								<td>{item.name}</td>
								<td>{item.rank}</td>
								<td className={getTextColor(item._30)}>{item._30}%</td>
								<td className={getTextColor(item._90)}>{item._90}%</td>
								<td className={getTextColor(item._180)}>{item._180}%</td>
								{props.isAdmin &&
									<td>
										{item.hidden &&
											<Button variant='warning' disabled={isLoading} onClick={() => toggleHidden(item.name)}>Show</Button>
										}
										{item.hidden === false &&
											<Button variant='success' disabled={isLoading} onClick={() => toggleHidden(item.name)}>Hide</Button>
										}
									</td>
								}
								{props.isAdmin &&
									<td>
										{!item.admin &&
											<Button variant='success' disabled={isLoading} onClick={() => toggleAdmin(item.name)}>Enable</Button>
										}
										{item.admin &&
											<Button variant='danger' disabled={isLoading} onClick={() => toggleAdmin(item.name)}>Disable</Button>
										}
									</td>
								}
							</tr>
						)}
					</tbody>
				</Table>
			</>
		</>
	);
}