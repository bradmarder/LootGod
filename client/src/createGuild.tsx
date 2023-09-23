import { useState } from 'react';
import { Alert, Button, Form } from 'react-bootstrap';
import axios from 'axios';
import servers from './servers';

export default function CreateGuild(props: { finish: () => void }) {

	const [isLoading, setIsLoading] = useState(false);
	const [LeaderName, setLeaderName] = useState('');
	const [GuildName, setGuildName] = useState('');
	const [Server, setServer] = useState('');

	const createGuild = () => {
		setIsLoading(true);
		const data = { LeaderName, GuildName, Server: Number(Server) };
		axios
			.post<string>('/CreateGuild', data)
			.then(x => {
				axios.defaults.headers.common['Player-Key'] = x.data;
				localStorage.setItem('key', x.data);
				props.finish();
			})
			.finally(() => setIsLoading(false));
	};

	return (
		<Alert variant='dark'>
			<Form onSubmit={e => { e.preventDefault(); createGuild(); }}>
				<Form.Group>
					<Form.Label>Leader Name</Form.Label>
					<Form.Control type="text" placeholder='Enter leader name' value={LeaderName} onChange={e => setLeaderName(e.target.value)} />
				</Form.Group>
				<Form.Group>
					<Form.Label>Guild Name</Form.Label>
					<Form.Control type="text" placeholder='Enter guild name' value={GuildName} onChange={e => setGuildName(e.target.value)} />
				</Form.Group>
				<Form.Group>
					<Form.Label>Server</Form.Label>
					<Form.Select value={Server} onChange={e => setServer(e.target.value)}>
						<option value=''>Select Server</option>
						{[...servers].map(([k, v]) =>
							<option key={k} value={k}>{v}</option>
						)}
					</Form.Select>
				</Form.Group>
				<br />
				<Button variant='success' disabled={isLoading || LeaderName.length < 4 || GuildName.length < 4 || Server === ''} onClick={createGuild}>Create Guild/Leader</Button>
			</Form>
		</Alert>
	);
}