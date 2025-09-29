import { useState, useEffect } from 'react';
import { Alert, Button, Form, ListGroup } from 'react-bootstrap';
import axios from 'axios';

export default function LinkAlt(props: { refreshLinkedAltsCacheKey: () => void }) {

	const [loading, setLoading] = useState(true);
	const [altName, setAltName] = useState('');
	const [result, setResult] = useState<number | undefined>();
	const [linkedAlts, setLinkedAlts] = useState<string[]>([]);
	const [cache, setCache] = useState(0);

	const linkAlt = () => {
		setResult(undefined);
		setLoading(true);
		axios
			.post<number>('/LinkAlt?altName=' + altName)
			.then(x => {
				setResult(x.data);
				setAltName('');
				setCache(x => x + 1);
				props.refreshLinkedAltsCacheKey();
			})
			.finally(() => setLoading(false));
	};
	const unlinkAlt = (name: string) => {
		setLoading(true);
		axios
			.post('/UnlinkAlt?altName=' + name)
			.then(() => {
				setCache(x => x + 1);
				props.refreshLinkedAltsCacheKey();
			})
			.finally(() => setLoading(false));
	}

	useEffect(() => {
		setLoading(true);
		const ac = new AbortController();
		axios
			.get<string[]>('/GetLinkedAlts', { signal: ac.signal })
			.then(x => setLinkedAlts(x.data))
			.finally(() => setLoading(false));
		return () => ac.abort();
	}, [cache]);

	return (
		<Alert variant='dark'>
			<h5>Currently Linked Alts</h5>
			<ListGroup>
				{linkedAlts.map(x =>
					<ListGroup.Item key={x}>
						{x}
						<Button variant='danger' size='sm' className='float-end' disabled={loading} onClick={() => unlinkAlt(x)}>Unlink</Button>
					</ListGroup.Item>
				)}
			</ListGroup>
			<hr />
			<Form onSubmit={e => { e.preventDefault(); linkAlt(); }}>
				<Form.Group>
					<Form.Label>Link Alt by Name</Form.Label>
					<Form.Control type="text" placeholder='Enter alt name' value={altName} onChange={e => setAltName(e.target.value)} />
				</Form.Group>
				<br />
				<Button variant='success' disabled={loading || altName.length < 4} onClick={linkAlt}>Link</Button>
			</Form>
			{result === 1 &&
				<Alert variant='success'>Successfully linked alt. You earn raid credit on your main for any raid dumps that include your alt.</Alert>
			}
			{result === 0 &&
				<Alert variant='danger'>Failed to link alt. Either you spelled the name wrong, or they aren't in the guild, or someone has already linked this alt, or they are not flagged as an "alt" in the EQ UI guild window.</Alert>
			}
		</Alert>
	);
}