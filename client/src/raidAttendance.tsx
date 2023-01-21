import { useState, useEffect } from 'react';
import './App.css';
import { Table, Button } from 'react-bootstrap';
import 'bootstrap/dist/css/bootstrap.min.css';
import axios from 'axios';

export function RaidAttendance(props: { isAdmin: boolean }) {

	const [isLoading, setIsLoading] = useState(true);
	const [ra, setRa] = useState<IRaidAttendance[]>([]);

	const getRA = async () => {
		const res = await axios.get<IRaidAttendance[]>('/GetPlayerAttendance');
		setRa(res.data);
		setIsLoading(false);
	};

	const getTextColor = (ra: number) => {
		return ra >= 70 ? 'text-success'
			: ra >= 50 ? 'text-warning'
			: 'text-danger';
	};

	const toggleHidden = async (name: string) => {
		setIsLoading(true);
		try {
			await axios.post('/ToggleHiddenPlayer?playerName=' + name);
			await getRA();
		}
		finally {
			setIsLoading(false);
		}
	};

	useEffect(() => {
		getRA();
	}, []);

	return (
		<>
			<h3>Raid Attendance</h3>
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
						</tr>
					</thead>
					<tbody>
						{ra.filter(x => props.isAdmin ? true : !x.hidden).map((item, i) =>
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
							</tr>
						)}
					</tbody>
				</Table>
			</>
		</>
	);
}