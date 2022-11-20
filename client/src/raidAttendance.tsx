import { useState, useEffect } from 'react';
import './App.css';
import { Table } from 'react-bootstrap';
import 'bootstrap/dist/css/bootstrap.min.css';
import axios from 'axios';

const api = process.env.REACT_APP_API_PATH;

export function RaidAttendance() {

	const [isLoading, setIsLoading] = useState(true);
	const [ra, setRa] = useState<IRaidAttendance[]>([]);

	const getRA = async () => {
		const res = await axios.get<IRaidAttendance[]>(api + '/GetPlayerAttendance');
		setRa(res.data);
		setIsLoading(false);
	};

	const getTextColor = (ra: number) => {
		return ra >= 70 ? 'text-success'
			: ra >= 50 ? 'text-warning'
			: 'text-danger';
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
							<th>30 Days</th>
							<th>90 Days</th>
							<th>180 Days</th>
						</tr>
					</thead>
					<tbody>
						{ra.map((item, i) =>
							<tr key={item.name}>
								<td>{item.name}</td>
								<td className={getTextColor(item._30)}>{item._30}%</td>
								<td className={getTextColor(item._90)}>{item._90}%</td>
								<td className={getTextColor(item._180)}>{item._180}%</td>
							</tr>
						)}
					</tbody>
				</Table>
			</>
		</>
	);
}