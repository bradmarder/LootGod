import { useEffect, useState } from 'react';
import './App.css';
import { Container, Row, Col, Alert, Button } from 'react-bootstrap';
import 'bootstrap/dist/css/bootstrap.min.css';
import axios from 'axios';
import { HubConnectionBuilder } from '@microsoft/signalr';
import { Login } from './login';
import { LootRequests } from './lootRequests';
import { CreateLootRequest } from './createLootRequest';
import { CreateLoot } from './createLoot';
import Loots from './loots';
import { GrantedLoots } from './grantedLoots';
import { ArchivedLoot } from './archivedLoot';
import { RaidAttendance } from './raidAttendance';

const api = process.env.REACT_APP_API_PATH;
const name = localStorage.getItem('name');
const admin = localStorage.getItem('admin');

export default function App() {
	const [loading, setLoading] = useState(false);
	const [isAdmin, setIsAdmin] = useState(admin == 'true');
	const [isReady, setIsReady] = useState(name != null);
	const [lootLock, setLootLock] = useState(false);
	const [mainName, setMainName] = useState(name || '');
	const [requests, setRequests] = useState<ILootRequest[]>([]);
	const [loots, setLoots] = useState<ILoot[]>([]);

	const getLoots = async (signal: AbortSignal) => {
		const res = await axios.get<ILoot[]>(api + '/GetLoots', { signal });
		setLoots(res.data);
	};
	const getLootRequests = async (signal: AbortSignal) => {
		const res = await axios.get<ILootRequest[]>(api + '/GetLootRequests', { signal });
		setRequests(res.data);
	};
	const getLootLock = async (signal: AbortSignal) => {
		const res = await axios.get<boolean>(api + '/GetLootLock', { signal });
		setLootLock(res.data);
	};
	const enableLootLock = async () => {
		setLoading(true);
		try {
			await axios.post(api + '/EnableLootLock');
		}
		finally {
			setLoading(false);
		}
	};
	const disableLootLock = async () => {
		setLoading(true);
		try {
			await axios.post(api + '/DisableLootLock');
		}
		finally {
			setLoading(false);
		}
	};

	useEffect(() => {
		const controller = new AbortController();

		getLoots(controller.signal);
		getLootLock(controller.signal);
		getLootRequests(controller.signal);

		return () => controller.abort();
	}, []);

	useEffect(() => {
		const connection = new HubConnectionBuilder()
			.withUrl(api + "/lootHub")
			.configureLogging(2) // signalR.LogLevel.Information
			.withAutomaticReconnect()
			.build();

		connection.on("lock", (lootLock: boolean) => {
			setLootLock(lootLock);
		});
		connection.on("loots", (loots: ILoot[]) => {
			setLoots(loots);
		});
		connection.on("requests", (requests: ILootRequest[]) => {
			setRequests(requests);
		});
		connection.onclose(error => {
			alert('Connection to the server has dropped. Can you reload the page please? Thank you.');
		});

		connection.start();

		return () => { connection.stop(); }
	}, []);

	const logout = () => {
		localStorage.removeItem('name');
		setMainName('');
		setIsReady(false);
	};
	const finishLogin = (name: string, admin: boolean) => {
		setMainName(name);
		setIsAdmin(admin);
		setIsReady(true);
	};

	return (
		<Container fluid>
			<h1>{process.env.REACT_APP_TITLE}</h1>
			{!isReady &&
				<Login finishLogin={finishLogin} />
			}
			{isReady &&
				<Row>
					<Col xs={12} xl={6}>
						<CreateLootRequest requests={requests} loots={loots} mainName={mainName} isAdmin={isAdmin} lootLocked={lootLock}></CreateLootRequest>
						<br />
						<LootRequests requests={requests} loots={loots} mainName={mainName} isAdmin={isAdmin} lootLocked={lootLock}></LootRequests>
						<br />
						{isAdmin &&
							<GrantedLoots requests={requests} loots={loots} mainName={mainName} isAdmin={isAdmin} lootLocked={lootLock}></GrantedLoots>
						}
						{isAdmin &&
							<ArchivedLoot requests={requests} loots={loots} mainName={mainName} isAdmin={isAdmin} lootLocked={lootLock}></ArchivedLoot>
						}
					</Col>
					<Col xs={12} xl={6}>
						<Alert variant='secondary'>
							Logged in as <strong>{mainName}</strong>
							<br />
							<Button onClick={logout}>Logout</Button>
						</Alert>
						{isAdmin &&
							<>
								<CreateLoot loots={loots}></CreateLoot>
								{lootLock &&
									<Button variant={'success'} onClick={disableLootLock} disabled={loading}>Unlock/Enable Loot Requests</Button>
								}
								{!lootLock &&
									<Button variant={'danger'} onClick={enableLootLock} disabled={loading}>Lock/Disable Loot Requests</Button>
								}
								<br /><br />
							</>
						}
						<Loots requests={requests} loots={loots} mainName={mainName} isAdmin={isAdmin} spell={true}></Loots>
						<br />
						<Loots requests={requests} loots={loots} mainName={mainName} isAdmin={isAdmin} spell={false}></Loots>
						<br />
						<RaidAttendance isAdmin={isAdmin}></RaidAttendance>
					</Col>
				</Row>
			}
		</Container>
	);
}